using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GersangTracker.Models;
using GersangTracker.Services;
using System.Collections.ObjectModel;
using System.Windows.Threading;

namespace GersangTracker.ViewModels
{
    public partial class HuntingViewModel : ObservableObject
    {
        private readonly DatabaseService _databaseService;
        private readonly OcrService _ocrService;

        // UI 스레드에서 동작하는 타이머 (경과시간 업데이트용)
        private readonly DispatcherTimer _dispatcherTimer;

        // 사냥 시작 시간
        private DateTime _startTime;

        // 현재 세션 ID
        private int _sessionId;

        // 사냥 중인 몬스터
        public Monster CurrentMonster { get; }

        // 경과 시간 표시
        [ObservableProperty]
        private string _elapsedTime = "00:00:00";

        // 연결 상태 로그
        public ObservableCollection<string> StatusLogs { get; } = new();

        // 실시간 드랍 로그 목록
        public ObservableCollection<DropLog> DropLogs { get; } = new();

        // 아이템 합산 목록 (아이템명 → 총수량)
        public ObservableCollection<ItemSummary> ItemSummaries { get; } = new();

        public HuntingViewModel(Monster monster)
        {
            CurrentMonster = monster;
            _databaseService = new DatabaseService();
            _ocrService = new OcrService();

            // OcrService 이벤트 구독 - 새 아이템 감지시 OnItemDropped 호출
            _ocrService.ItemDropped += OnItemDropped;

            // OcrService 창 감지 이벤트 구독
            _ocrService.WindowDetected += (s, detected) =>
            {
                AddStatusLog(detected ? "거상 창 감지됨 ✔" : "거상 창을 찾을 수 없음 ✖");
            };
            _ocrService.TextRecognized += (s, text) =>
            {
                AddStatusLog($"OCR : {text}");
            };

            // DispatcherTimer 설정 - 1초마다 경과시간 업데이트
            _dispatcherTimer = new DispatcherTimer();
            _dispatcherTimer.Interval = TimeSpan.FromSeconds(1);
            _dispatcherTimer.Tick += OnTimerTick;
        }

        // 사냥 시작
        public async Task StartAsync()
        {
            _startTime = DateTime.Now;
            _sessionId = await _databaseService.AddSessionAsync(CurrentMonster.Id, _startTime);
            _dispatcherTimer.Start();
            _ocrService.Start();

            AddStatusLog("OCR 시작됨");
            AddStatusLog("거상 창 감지 중...");

            App.Current.Dispatcher.Invoke(() =>
            {
                DropLogs.Insert(0, new DropLog { ItemName = "화염석", Quantity = 1, DroppedAt = DateTime.Now });
                DropLogs.Insert(0, new DropLog { ItemName = "거월부", Quantity = 2, DroppedAt = DateTime.Now });
                ItemSummaries.Add(new ItemSummary { ItemName = "화염석", TotalQuantity = 1 });
                ItemSummaries.Add(new ItemSummary { ItemName = "거월부", TotalQuantity = 2 });
            });
        }

        // 1초마다 경과시간 업데이트
        private void OnTimerTick(object? sender, EventArgs e)
        {
            var elapsed = DateTime.Now - _startTime;
            ElapsedTime = elapsed.ToString(@"hh\:mm\:ss");
        }

        // 아이템 드랍 감지시 호출
        private async void OnItemDropped(object? sender, DroppedItemEventArgs e)
        {
            // DB에 드랍 로그 저장
            await _databaseService.AddDropLogAsync(_sessionId, e.ItemName, e.Quantity);

            // UI 스레드에서 목록 업데이트
            App.Current.Dispatcher.Invoke(() =>
            {
                // 실시간 드랍 로그 추가
                DropLogs.Insert(0, new DropLog
                {
                    ItemName = e.ItemName,
                    Quantity = e.Quantity,
                    DroppedAt = e.DroppedAt
                });

                // 아이템 합산 업데이트
                var existing = ItemSummaries.FirstOrDefault(s => s.ItemName == e.ItemName);
                if (existing != null)
                    existing.TotalQuantity += e.Quantity;
                else
                    ItemSummaries.Add(new ItemSummary
                    {
                        ItemName = e.ItemName,
                        TotalQuantity = e.Quantity
                    });
            });
        }

        // 사냥 종료
        public async Task<int> StopAsync()
        {
            // 타이머 정지
            _dispatcherTimer.Stop();

            // OCR 정지
            _ocrService.Stop();
            _ocrService.Dispose();

            // DB 세션 업데이트
            await _databaseService.UpdateSessionAsync(_sessionId, DateTime.Now, 0);

            return _sessionId;
        }

        private void AddStatusLog(string message)
        {
            App.Current.Dispatcher.Invoke(() =>
            {
                StatusLogs.Insert(0, $"[{DateTime.Now:HH:mm:ss}] {message}");
            });
        }

    }

    // 아이템 합산 모델 (화면 표시용)
    public partial class ItemSummary : ObservableObject
    {
        public string ItemName { get; set; } = string.Empty;

        [ObservableProperty]
        private int _totalQuantity;
    }
}