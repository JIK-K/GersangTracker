using OpenCvSharp;
using OpenCvSharp.Extensions;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text.RegularExpressions;
using System.Timers;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;

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
        [DllImport("gdi32.dll")]
        static extern bool BitBlt(IntPtr hdcDest, int nXDest, int nYDest,
            int nWidth, int nHeight, IntPtr hdcSrc, int nXSrc, int nYSrc, uint dwRop);

        // Windows API가 창 영역을 반환할 때 사용하는 구조체
        [StructLayout(LayoutKind.Sequential)]
        struct RECT { public int Left, Top, Right, Bottom; }
        #endregion

        // 새 아이템 드랍 감지시 외부에 알려주는 이벤트
        public event EventHandler<DroppedItemEventArgs>? ItemDropped;

        // 거상 창 감지 상태 변경시 알림
        public event EventHandler<bool>? WindowDetected;

        // OCR 텍스트 인식 결과 알림 (디버그용)
        public event EventHandler<string>? TextRecognized;

        // 정규식 - [닉네임]님이 [아이템명] N개를 획득하였습니다
        private readonly Regex _dropRegex = new(@"\[(.+?)\]님.{1,2}\[(.+?)\]\s*(\d+)\s*개를\s*획득하였습니다\.?");

        // 이전 캡처 줄 목록 - 신규 드랍 판별에 사용
        private List<string> _prevLines = new();

        // 1초마다 캡처를 실행하는 타이머
        private readonly System.Timers.Timer _timer;

        // Windows OCR 엔진 - 한국어
        private readonly OcrEngine _ocrEngine;

        // 캡처 영역 설정
        private readonly int _startX = 0;
        private readonly int _startY = 55;
        private readonly int _cropWidth = 400;
        private readonly int _cropHeight = 200;

        // 거상 창 감지 상태
        private bool _wasWindowFound = false;

        public OcrService()
        {
            // Windows OCR 한국어 엔진 초기화
            var language = new Windows.Globalization.Language("ko");
            _ocrEngine = OcrEngine.TryCreateFromLanguage(language)
                ?? OcrEngine.TryCreateFromUserProfileLanguages()
                ?? throw new Exception("한국어 OCR 엔진을 초기화할 수 없습니다. 한국어 언어팩을 설치해주세요.");

            // 1초 타이머 설정
            _timer = new System.Timers.Timer(1000);
            _timer.Elapsed += OnTimerElapsed;
        }

        // 사냥 시작 - 타이머 시작
        public void Start()
        {
            _prevLines.Clear();
            _timer.Start();
        }

        // 사냥 종료 - 타이머 정지
        public void Stop()
        {
            _timer.Stop();
            _prevLines.Clear();
        }

        // 1초마다 실행되는 캡처 로직
        private async void OnTimerElapsed(object? sender, ElapsedEventArgs e)
        {
            try
            {
                // 1. 거상 창 찾기
                IntPtr hwnd = FindWindow(null!, "Gersang");

                // 창 감지 상태 변경시에만 이벤트 발생
                if (hwnd == IntPtr.Zero && _wasWindowFound)
                {
                    _wasWindowFound = false;
                    WindowDetected?.Invoke(this, false);
                    return;
                }
                else if (hwnd != IntPtr.Zero && !_wasWindowFound)
                {
                    _wasWindowFound = true;
                    WindowDetected?.Invoke(this, true);
                }

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

                // 이미지 2배 확대
                Bitmap enlarged = new Bitmap(cropped.Width * 2, cropped.Height * 2);
                using (Graphics g = Graphics.FromImage(enlarged))
                {
                    g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                    g.DrawImage(cropped, 0, 0, enlarged.Width, enlarged.Height);
                }

                // @Debug - 캡처 이미지 저장
                enlarged.Save(@"C:\temp\hunting_crop.png");

                // 5. Bitmap → SoftwareBitmap 변환 (Windows OCR용)
                SoftwareBitmap softwareBitmap = BitmapToSoftwareBitmap(enlarged);

                // 6. Windows OCR 실행
                OcrResult result = await _ocrEngine.RecognizeAsync(softwareBitmap);
                string text = string.Join("\n", result.Lines.Select(l => l.Text));

                // @Debug
                if (!string.IsNullOrWhiteSpace(text))
                    TextRecognized?.Invoke(this, text);

                // 7. 줄 단위로 분리
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
                enlarged.Dispose();
                softwareBitmap.Dispose();
            }
            catch (Exception ex)
            {
                TextRecognized?.Invoke(this, $"오류: {ex.Message}");
            }
        }

        // Bitmap → SoftwareBitmap 변환
        private SoftwareBitmap BitmapToSoftwareBitmap(Bitmap bitmap)
        {
            // Bitmap을 BGRA8 포맷으로 변환
            Bitmap bmp = new(bitmap.Width, bitmap.Height, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(bmp))
                g.DrawImage(bitmap, 0, 0);

            BitmapData data = bmp.LockBits(
                new Rectangle(0, 0, bmp.Width, bmp.Height),
                ImageLockMode.ReadOnly,
                PixelFormat.Format32bppArgb);

            byte[] bytes = new byte[data.Stride * data.Height];
            Marshal.Copy(data.Scan0, bytes, 0, bytes.Length);
            bmp.UnlockBits(data);

            SoftwareBitmap softwareBitmap = new(
                BitmapPixelFormat.Bgra8,
                bmp.Width,
                bmp.Height,
                BitmapAlphaMode.Premultiplied);

            softwareBitmap.CopyFromBuffer(bytes.AsBuffer());

            return softwareBitmap;
        }

        // 신규 줄 판별 로직
        private List<string> GetNewLines(List<string> prev, List<string> current)
        {
            if (current.Count == 0) return new();
            if (prev.Count == 0) return new(current);

            int matchIndex = -1;
            for (int i = 0; i < current.Count; i++)
            {
                if (current[i] == prev[0])
                {
                    matchIndex = i;
                    break;
                }
            }

            if (matchIndex == -1) return new(current);
            return current.Take(matchIndex).ToList();
        }

        // BitBlt 방식으로 게임 창 캡처
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