using OpenCvSharp;
using OpenCvSharp.Extensions;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Timers;
using Tesseract;

namespace GersangTracker.Services
{

    public class DroppedItemEventArgs : EventArgs
    {
        public string ItemName { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public DateTime DroppedAt { get; set; }
    }

    public class OcrService : IDisposable
    {
        #region Windows API
        // 창 제목으로 게임 창 핸들(고유 ID)을 찾아오는 API
        [DllImport("user32.dll")]
        static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        // 창 핸들로 창의 위치와 크기를 가져오는 API
        [DllImport("user32.dll")]
        static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        // 창의 그래픽 컨텍스트(DC)를 가져오는 API - 캡처 준비 단계
        [DllImport("user32.dll")]
        static extern IntPtr GetDC(IntPtr hWnd);

        // 가져온 그래픽 컨텍스트(DC)를 해제하는 API - 캡처 후 반드시 호출
        [DllImport("user32.dll")]
        static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

        // 실제 화면을 캡처하는 API
        // hdcDest: 저장할 곳, hdcSrc: 캡처할 창, dwRop: 복사 방식 (0x00CC0020 = 그대로 복사)
        [DllImport("gdi32.dll")]
        static extern bool BitBlt(IntPtr hdcDest, int nXDest, int nYDest,
            int nWidth, int nHeight, IntPtr hdcSrc, int nXSrc, int nYSrc, uint dwRop);

        // Windows API가 창 영역을 반환할 때 사용하는 구조체
        // StructLayout: C#과 Windows API의 메모리 구조를 맞추기 위한 설정
        [StructLayout(LayoutKind.Sequential)]
        struct RECT { public int Left, Top, Right, Bottom; }
        #endregion

        // 새 아이템 드랍 감지시 외부(HuntingViewModel)에 알려주는 이벤트
        // ? 는 구독자가 없을 때 null 허용
        public event EventHandler<DroppedItemEventArgs>? ItemDropped;

        // [닉네임]님이 [아이템명] N개를 획득하였습니다 패턴 파싱
        private readonly Regex _dropRegex = new(@"\[(.+?)\]님이 \[(.+?)\] (\d+)개를 획득하였습니다");

        // 이전 캡처 줄 목록 - 신규 드랍 판별에 사용
        private List<string> _prevLines = new();

        // 1초마다 캡처를 실행하는 타이머
        private readonly System.Timers.Timer _timer;

        // Tesseract 언어 데이터 경로
        private readonly string _tessPath;

        // 캡처 영역 설정 (게임 창 기준 좌표)
        private readonly int _startX = 0;       // 캡처 시작 X
        private readonly int _startY = 0;       // 캡처 시작 Y
        private readonly int _cropWidth = 400;  // 캡처 너비
        private readonly int _cropHeight = 200; // 캡처 높이

        public OcrService()
        {
            // Assets/tessdata 폴더 경로 설정
            _tessPath = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "Assets", "tessdata");

            // 1초 타이머 설정
            _timer = new System.Timers.Timer(1000);
            _timer.Elapsed += OnTimerElapsed;
        }

        public void Start()
        {
            _prevLines.Clear();
            _timer.Start();
        }


        public void Stop()
        {
            _timer.Stop();
            _prevLines.Clear();
        }


        private void OnTimerElapsed(object? sender, ElapsedEventArgs e)
        {
            try
            {
                // 1. 거상 창 찾기
                IntPtr hwnd = FindWindow(null!, "Gersang");
                if (hwnd == IntPtr.Zero) return;

                // 2. 창 크기 가져오기
                GetWindowRect(hwnd, out RECT rect);
                int windowWidth = rect.Right - rect.Left;
                int windowHeight = rect.Bottom - rect.Top;

                // 3. 창 전체 캡처
                Bitmap fullCapture = CaptureWindow(hwnd, windowWidth, windowHeight);

                // 4. 드랍 메시지 영역만 Crop
                Rectangle cropRect = new(_startX, _startY, _cropWidth, _cropHeight);
                Bitmap cropped = fullCapture.Clone(cropRect, fullCapture.PixelFormat);

                // 5. 전처리 - 그레이스케일 → 이진화 (OCR 정확도 향상)
                Mat mat = BitmapConverter.ToMat(cropped);
                Mat gray = new();
                Mat binary = new();
                Cv2.CvtColor(mat, gray, ColorConversionCodes.BGR2GRAY);
                Cv2.Threshold(gray, binary, 180, 255, ThresholdTypes.Binary);
                Bitmap processed = BitmapConverter.ToBitmap(binary);

                // 6. Tesseract OCR 실행
                using var engine = new TesseractEngine(_tessPath, "kor", EngineMode.Default);
                using var img = Pix.LoadTiffFromMemory(Array.Empty<byte>());
                using var page = engine.Process(img);
                string text = page.GetText().Trim();

                // 7. 줄 단위로 분리 후 공백 제거
                List<string> currentLines = text
                    .Split('\n')
                    .Select(l => l.Trim())
                    .Where(l => !string.IsNullOrWhiteSpace(l))
                    .ToList();

                // 8. 이전 캡처와 비교해서 신규 줄만 추출
                List<string> newLines = GetNewLines(_prevLines, currentLines);

                // 9. 신규 줄에서 아이템 파싱 후 이벤트 발생
                foreach (var line in newLines)
                {
                    var match = _dropRegex.Match(line);
                    if (match.Success)
                    {
                        // ItemDropped 이벤트 발생 - 구독자(HuntingViewModel)에게 알림
                        ItemDropped?.Invoke(this, new DroppedItemEventArgs
                        {
                            ItemName = match.Groups[2].Value,
                            Quantity = int.Parse(match.Groups[3].Value),
                            DroppedAt = DateTime.Now
                        });
                    }
                }

                // 10. 현재 줄 목록을 이전 목록으로 저장
                _prevLines = currentLines;

                // 11. 리소스 해제
                fullCapture.Dispose();
                cropped.Dispose();
                processed.Dispose();
            }
            catch { }
        }



        private List<string> GetNewLines(List<string> prev, List<string> current)
        {
            // 현재 캡처가 비어있으면 신규 없음
            if (current.Count == 0) return new();

            // 이전 캡처가 비어있으면 현재 전체가 신규
            if (prev.Count == 0) return new(current);

            // 이전 첫줄이 현재 몇번째 인덱스에 있는지 찾기
            int matchIndex = -1;
            for (int i = 0; i < current.Count; i++)
            {
                if (current[i] == prev[0])
                {
                    matchIndex = i;
                    break;
                }
            }

            // 이전 첫줄이 현재에 없으면 현재 전체가 신규
            if (matchIndex == -1) return new(current);

            // 매칭된 인덱스 위의 줄들만 신규
            return current.Take(matchIndex).ToList();
        }

        private Bitmap CaptureWindow(IntPtr hwnd, int width, int height)
        {
            IntPtr hdcSrc = GetDC(hwnd);
            Bitmap bitmap = new(width, height, PixelFormat.Format32bppArgb);
            using Graphics g = Graphics.FromImage(bitmap);
            IntPtr hdcDest = g.GetHdc();
            BitBlt(hdcDest, 0, 0, width, height, hdcSrc, 0, 0, 0x00CC0020);
            g.ReleaseHdc(hdcDest);
            ReleaseDC(hwnd, hdcSrc);
            return bitmap;
        }


        public void Dispose()
        {
            _timer.Dispose();
        }
    }
}