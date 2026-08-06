using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GersangTracker.Models;
using GersangTracker.Services;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace GersangTracker.ViewModels
{
    public partial class HuntingViewModel : ObservableObject
    {
        private readonly DatabaseService _databaseService;
        private readonly PacketSnifferService _snifferService;

        // UI 스레드에서 동작하는 타이머 (경과시간 업데이트)    
        private readonly DispatcherTimer _dispatcherTimer;

        // 사냥 시작 시간
        private DateTime _startTime;
        private readonly Stopwatch _stopwatch = new Stopwatch();

        // 현재 세션 ID
        private int _sessionId;

        // 타겟 프로세스 (현재 탭의 클라이언트 설치 경로 기반)
        private int _targetPid = -1;
        private string _clientPath;

        // 사냥 중인 몬스터
        public Monster CurrentMonster { get; }

        // 경과 시간 표시
        [ObservableProperty]
        private string _elapsedTime = "00:00:00";

        [ObservableProperty]
        private bool _isPaused = false;

        // 연결 상태 로그
        public ObservableCollection<string> StatusLogs { get; } = new();

        // 실시간 드롭 로그 목록
        public ObservableCollection<DropLog> DropLogs { get; } = new();

        // 아이템 합산 목록 (아이템명 별 총 수량)
        public ObservableCollection<ItemSummary> ItemSummaries { get; } = new();

        // 생성자 매개변수에 clientPath 추가됨
        public HuntingViewModel(Monster monster, DatabaseService databaseService, PacketSnifferService snifferService, string clientPath)
        {
            CurrentMonster = monster;
            _databaseService = databaseService;
            _snifferService = snifferService;
            _clientPath = clientPath;

            FindTargetPid(); // 클라이언트 경로를 바탕으로 추적할 거상의 실제 PID 찾기

            // PacketSnifferService 이벤트 구독
            _snifferService.ItemDropped += OnItemDropped;
            _snifferService.StatusLog += (message) =>
            {
                AddStatusLog(message);
            };

            // DispatcherTimer 설정
            _dispatcherTimer = new DispatcherTimer();
            _dispatcherTimer.Interval = TimeSpan.FromSeconds(1);
            _dispatcherTimer.Tick += OnTimerTick;
        }

        private void FindTargetPid()
        {
            if (string.IsNullOrEmpty(_clientPath)) return;

            var processes = Process.GetProcessesByName("gersang");
            foreach (var p in processes)
            {
                try
                {
                    if (p.MainModule?.FileName.Equals(_clientPath, StringComparison.OrdinalIgnoreCase) == true)
                    {
                        _targetPid = p.Id;
                        AddStatusLog($"[클라이언트 매칭 성공] PID: {_targetPid}");
                        break;
                    }
                }
                catch
                {
                    // 접근 권한 부족 (System 권한 등)은 무시
                }
            }

            if (_targetPid == -1)
            {
                AddStatusLog($"[경고] '{_clientPath}' 경로로 실행된 거상을 찾지 못했습니다. 드랍 기록이 수집되지 않을 수 있습니다.");
            }
        }

        // 일시정지/재개 커맨드
        [RelayCommand]
        private void TogglePause()
        {
            if (IsPaused)
            {
                _stopwatch.Start();
                _dispatcherTimer.Start();
                IsPaused = false;
            }
            else
            {
                _stopwatch.Stop();
                _dispatcherTimer.Stop();
                IsPaused = true;
            }
        }

        // 사냥 시작
        public async Task StartAsync()
        {
            _startTime = DateTime.Now;
            _sessionId = await _databaseService.AddSessionAsync(CurrentMonster.Id, _startTime);

            _stopwatch.Start();
            _dispatcherTimer.Start();
            _snifferService.Start();

            AddStatusLog("[시작됨]");
        }

        // 1초마다 경과시간 업데이트
        private void OnTimerTick(object? sender, EventArgs e)
        {
            ElapsedTime = _stopwatch.Elapsed.ToString(@"hh\:mm\:ss");
        }

        // 아이템 드롭 감지 시 호출
        private async void OnItemDropped(object? sender, DroppedItemEventArgs e)
        {
            // 이 패킷 이벤트가 내가 추적하는 거상 클라이언트(PID)에서 온 것이 아니라면 무시! (완벽 분리 핵심)
            if (_targetPid != -1 && e.Pid != _targetPid)
                return;

            // DB에 드롭 로그 저장
            await _databaseService.AddDropLogAsync(_sessionId, e.ItemName, e.Quantity);

            // UI 스레드에서 목록 업데이트
            App.Current.Dispatcher.Invoke(() =>
            {
                // 실시간 드롭 로그 추가
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
            _dispatcherTimer.Stop();
            _stopwatch.Stop();
            // _snifferService.Stop()은 여기서 호출하지 않습니다!
            // 다른 탭도 같은 스니퍼를 쓰고 있기 때문에 전역 스니퍼를 끄면 안됩니다.

            // 본인 이벤트만 리스너 해제 (메모리 릭 방지)
            _snifferService.ItemDropped -= OnItemDropped;

            var endTime = _startTime + _stopwatch.Elapsed;
            await _databaseService.UpdateSessionAsync(_sessionId, endTime, 0);

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