using OpenCvSharp;
using OpenCvSharp.Extensions;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
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
        // @Debug
        private readonly string _logDirectory = @"C:\temp";
        private readonly string _logFileName = "ocr_logs.txt";

        #region Windows API
        // 창 제목으로 게임 창 핸들(고유 ID)을 찾아오는 API
        [DllImport("user32.dll")]
        static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        // 창 핸들로 창의 위치와 크기를 가져오는 API
        [DllImport("user32.dll")]
        static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        // 창의 그래픽 컨텍스트(DC)를 가져오는 API
        [DllImport("user32.dll")]
        static extern IntPtr GetDC(IntPtr hWnd);

        // 그래픽 컨텍스트(DC)를 해제하는 API
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
        //private readonly Regex _dropRegex = new(@"\[(.+?)\]님.{1,3}\[(.+?)\]\s*(\d+)\s*개를\s*획득하");
        //private readonly Regex _dropRegex = new(@"\[.+?\][^\[]*\[([가-힣\s]+)\]\s*(\d+)");
        private readonly Regex _dropRegex = new(@"\[.+?\].*?\[([^\]]+)\]");

        // 이전 캡처 줄 목록 - 신규 드랍 판별에 사용
        private List<string> _prevLines = new();

        // 1초마다 캡처를 실행하는 타이머
        private readonly System.Timers.Timer _timer;

        // Tesseract 언어 데이터 경로
        private readonly string _tessPath;

        // 캡처 영역 설정
        private readonly int _startX = 0;
        private readonly int _startY = 55;
        private readonly int _cropWidth = 400;
        private readonly int _cropHeight = 200;

        // 거상 창 감지 상태
        private bool _wasWindowFound = false;

        public OcrService()
        {
            _tessPath = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "Assets", "tessdata");

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
        private void OnTimerElapsed(object? sender, ElapsedEventArgs e)
        {
            try
            {
                // 1. 거상 창 찾기
                IntPtr hwnd = FindWindow(null!, "Gersang");

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

                // 5. 이미지 2배 확대
                Bitmap enlarged = new Bitmap(cropped.Width * 2, cropped.Height * 2);
                using (Graphics g = Graphics.FromImage(enlarged))
                {
                    g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                    g.DrawImage(cropped, 0, 0, enlarged.Width, enlarged.Height);
                }

                // 6. 전처리 - 그레이스케일 → 이진화
                Mat mat = BitmapConverter.ToMat(enlarged);
                Mat gray = new();
                Mat binary = new();
                Cv2.CvtColor(mat, gray, ColorConversionCodes.BGR2GRAY);
                Cv2.Threshold(gray, binary, 180, 255, ThresholdTypes.Binary);
                Bitmap processed = BitmapConverter.ToBitmap(binary);

                // 7. Tesseract OCR 실행
                string tempPath = Path.Combine(Path.GetTempPath(), "ocr_temp.png");
                processed.Save(tempPath);

                using var engine = new TesseractEngine(_tessPath, "kor", EngineMode.Default);
                using var img = Pix.LoadFromFile(tempPath);
                using var page = engine.Process(img);
                string text = page.GetText().Trim();

                // 글자 사이 공백 제거
                text = Regex.Replace(text, @"(?<=\S) (?=\S)", "");
                text = text.Replace("{", "[").Replace("}", "]")
                           .Replace("(", "[").Replace(")", "]")
                           .Replace("【", "[").Replace("】", "]")
                           .Replace("〔", "[").Replace("〕", "]")
                           .Replace("「", "[").Replace("」", "]");

                // @Debug
                if (!string.IsNullOrWhiteSpace(text))
                {
                    SaveTextToFile(text);
                    TextRecognized?.Invoke(this, text);
                }

                // 8. 줄 단위로 분리
                List<string> currentLines = text
                    .Split('\n')
                    .Select(l => l.Trim())
                    .Where(l => !string.IsNullOrWhiteSpace(l))
                    .ToList();

                // 9. 이전 캡처와 비교해서 신규 줄만 추출
                List<string> newLines = GetNewLines(_prevLines, currentLines);

                // 10. 신규 줄에서 아이템 파싱 후 이벤트 발생
                foreach (var line in newLines)
                {
                    var match = _dropRegex.Match(line);
                    if (match.Success)
                    {
                        // 아이템명에서 한글만 추출 (오타 제거용)
                        string rawItemName = match.Groups[1].Value;
                        string itemName = Regex.Replace(rawItemName, @"[^가-힣]", "");

                        if (string.IsNullOrEmpty(itemName)) continue;

                        // 수량 파싱 시도 (숫자가 없으면 기본 1개로 처리)
                        var qtyMatch = Regex.Match(line.Substring(match.Index + match.Length), @"(\d+)");
                        int quantity = qtyMatch.Success ? int.Parse(qtyMatch.Groups[1].Value) : 1;

                        ItemDropped?.Invoke(this, new DroppedItemEventArgs
                        {
                            ItemName = itemName,
                            Quantity = quantity,
                            DroppedAt = DateTime.Now
                        });
                        //ItemDropped?.Invoke(this, new DroppedItemEventArgs
                        //{
                        //    ItemName = match.Groups[2].Value,
                        //    Quantity = int.Parse(match.Groups[3].Value),
                        //    DroppedAt = DateTime.Now
                        //});
                    }
                }

                // 11. 현재 줄 목록을 이전 목록으로 저장
                _prevLines = currentLines;

                // 12. 리소스 해제
                fullCapture.Dispose();
                cropped.Dispose();
                enlarged.Dispose();
                processed.Dispose();
                mat.Dispose();
                gray.Dispose();
                binary.Dispose();
            }
            catch (Exception ex)
            {
                TextRecognized?.Invoke(this, $"오류: {ex.Message}");
            }
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

        private void SaveTextToFile(string text)
        {
            try
            {
                // 1. 폴더 생성
                if (!Directory.Exists(_logDirectory))
                {
                    Directory.CreateDirectory(_logDirectory);
                }

                // 2. 파일명 결정 (파일명에는 : 를 쓸 수 없으므로 언더바(_)나 하이픈(-) 사용)
                // 예: C:\temp\ocr_log_2026-03-27.txt
                string fileName = $"ocr_log_{DateTime.Now:yyyy-MM-dd}.txt";
                string filePath = Path.Combine(_logDirectory, fileName);

                // 3. 로그 내용 구성
                StringBuilder sb = new StringBuilder();
                sb.AppendLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]");
                sb.AppendLine(text);
                sb.AppendLine(new string('-', 30));

                // 4. 파일 쓰기 (AppendAllText는 파일이 없으면 만들고, 있으면 이어붙입니다)
                File.AppendAllText(filePath, sb.ToString(), Encoding.UTF8);
            }
            catch (Exception ex)
            {
                // 권한 문제나 파일 사용 중 오류 발생 시 출력
                TextRecognized?.Invoke(this, $"파일 저장 실패: {ex.Message}");
            }
        }
    }
}