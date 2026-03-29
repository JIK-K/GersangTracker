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
        public event Action<string>? StatusLog;

        // 정규식 - 마지막 [] 안을 아이템명으로 파싱 (중첩 대괄호 대응)
        private readonly Regex _dropRegex = new(@"\[.+?\].*\[([^\]]+)\]");

        // 이전 캡처 줄 목록 - 신규 드랍 판별에 사용
        private List<string> _prevLines = new();

        // 직전 캡처에서 확정된 드랍 줄 목록 - 중복 방지용
        private List<string> _lastConfirmedLines = new();

        // 1초마다 캡처를 실행하는 타이머
        private readonly System.Timers.Timer _timer;

        // Tesseract 언어 데이터 경로
        private readonly string _tessPath;

        // 캡처 영역 설정
        private readonly int _startX = 0;
        private readonly int _startY = 60;
        private readonly int _cropWidth = 230;
        private readonly int _cropHeight = 130;

        // 거상 창 감지 상태
        private bool _wasWindowFound = false;

        // 레벤슈타인 매칭 대상 아이템 목록
        private List<string> _targetItems = new();
        private readonly List<string> _ocrLogs = new();

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
            _lastConfirmedLines.Clear();
            _timer.Start();
        }

        // 사냥 종료 - 타이머 정지
        public void Stop()
        {
            _timer.Stop();
            _prevLines.Clear();
            _lastConfirmedLines.Clear();
        }

        public void SetTargetItems(List<string> items)
        {
            _targetItems = items;
        }

        // OCR 결과 + 매칭/폐기/중복 모두 로그 파일 + 상태 로그에 기록
        private void Log(string message)
        {
            _ocrLogs.Add($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}");
            StatusLog?.Invoke(message);
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

                // 5. 이미지 3배 확대 (인식률 향상)
                Bitmap enlarged = new Bitmap(cropped.Width * 3, cropped.Height * 3);
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
                string tempPath = Path.Combine(_logDirectory, "ocr_temp.png");
                if (!Directory.Exists(_logDirectory))
                    Directory.CreateDirectory(_logDirectory);
                processed.Save(tempPath);

                using var engine = new TesseractEngine(_tessPath, "kor", EngineMode.Default);
                engine.SetVariable("tessedit_pageseg_mode", "6"); // 단일 블록 텍스트 모드
                using var img = Pix.LoadFromFile(tempPath);
                using var page = engine.Process(img);
                string text = page.GetText().Trim();

                // 8. 텍스트 후처리
                // 글자 사이 공백 제거
                text = Regex.Replace(text, @"(?<=\S) (?=\S)", "");

                // 대괄호 유사 문자 통일
                text = text.Replace("{", "[").Replace("}", "]")
                           .Replace("【", "[").Replace("】", "]")
                           .Replace("〔", "[").Replace("〕", "]")
                           .Replace("「", "[").Replace("」", "]")
                           .Replace("｢", "[").Replace("｣", "]");

                // @Debug - OCR 원문을 구분선과 함께 별도 블록으로 기록
                if (!string.IsNullOrWhiteSpace(text))
                {
                    _ocrLogs.Add($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [OCR]\n{text}\n{new string('-', 30)}");
                    TextRecognized?.Invoke(this, text);
                }

                // 9. 줄 단위로 분리
                List<string> currentLines = text
                    .Split('\n')
                    .Select(l => l.Trim())
                    .Where(l => !string.IsNullOrWhiteSpace(l))
                    .ToList();

                // 10. 이전 캡처와 비교해서 신규 줄만 추출
                List<string> newLines = GetNewLines(_prevLines, currentLines);

                // 11. 신규 줄에서 아이템 파싱 후 이벤트 발생
                List<string> confirmedLines = new();

                foreach (var line in newLines)
                {
                    var match = _dropRegex.Match(line);
                    if (match.Success)
                    {
                        // 아이템명에서 한글만 추출
                        string rawItemName = match.Groups[1].Value;
                        string cleaned = Regex.Replace(rawItemName, @"[^가-힣]", "");

                        if (string.IsNullOrEmpty(cleaned)) continue;

                        // 레벤슈타인 매칭
                        string? itemName = MatchToTarget(cleaned);
                        if (itemName == null) continue;

                        // 직전 캡처에서 이미 확정된 줄이면 중복 스킵
                        if (_lastConfirmedLines.Contains(line))
                        {
                            Log($"[중복스킵] {itemName}");
                            continue;
                        }

                        // 수량 파싱 (없으면 기본 1개)
                        var qtyMatch = Regex.Match(line.Substring(match.Index + match.Length), @"(\d+)");
                        int quantity = qtyMatch.Success ? int.Parse(qtyMatch.Groups[1].Value) : 1;

                        confirmedLines.Add(line);
                        Log($"[드랍확정] {itemName} x{quantity}");

                        ItemDropped?.Invoke(this, new DroppedItemEventArgs
                        {
                            ItemName = itemName,
                            Quantity = quantity,
                            DroppedAt = DateTime.Now
                        });
                    }
                }

                // 확정된 드랍 줄 갱신
                _lastConfirmedLines = confirmedLines;

                // 12. 현재 줄 목록을 이전 목록으로 저장
                _prevLines = currentLines;

                // 13. 리소스 해제
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
                Log($"[오류] {ex.Message}");
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

        // 레벤슈타인 거리 계산
        private static int GetLevenshteinDistance(string s, string t)
        {
            int n = s.Length, m = t.Length;
            var d = new int[n + 1, m + 1];

            for (int i = 0; i <= n; i++) d[i, 0] = i;
            for (int j = 0; j <= m; j++) d[0, j] = j;

            for (int i = 1; i <= n; i++)
                for (int j = 1; j <= m; j++)
                {
                    int cost = s[i - 1] == t[j - 1] ? 0 : 1;
                    d[i, j] = Math.Min(
                        Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                        d[i - 1, j - 1] + cost);
                }
            return d[n, m];
        }

        // OCR 결과 → 등록 아이템으로 매칭
        private string? MatchToTarget(string ocrResult)
        {
            if (_targetItems.Count == 0) return ocrResult;

            var best = _targetItems
                .Select(target => (target, dist: GetLevenshteinDistance(ocrResult, target)))
                .OrderBy(x => x.dist)
                .FirstOrDefault();

            // 임계값: 글자수의 50% 또는 최대 3 중 큰 값
            int threshold = Math.Max(3, (int)Math.Ceiling(ocrResult.Length * 0.5));

            if (best.dist <= threshold)
            {
                Log($"[매칭] {ocrResult} → {best.target} (거리:{best.dist}/임계:{threshold})");
                return best.target;
            }

            Log($"[폐기] {ocrResult} (거리:{best.dist} > 임계:{threshold})");
            return null;
        }

        public void SaveLogFile()
        {
            try
            {
                if (_ocrLogs.Count == 0) return;
                if (!Directory.Exists(_logDirectory))
                    Directory.CreateDirectory(_logDirectory);

                string fileName = $"ocr_log_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.txt";
                string filePath = Path.Combine(_logDirectory, fileName);

                File.WriteAllText(filePath, string.Join("\n", _ocrLogs), Encoding.UTF8);
                _ocrLogs.Clear();
            }
            catch (Exception ex)
            {
                StatusLog?.Invoke($"파일 저장 실패: {ex.Message}");
            }
        }

        public void Dispose()
        {
            _timer.Dispose();
        }
    }
}