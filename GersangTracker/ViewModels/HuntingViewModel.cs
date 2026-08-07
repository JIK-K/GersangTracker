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
using System.Collections.Concurrent;

namespace GersangTracker.ViewModels
{
    public partial class HuntingViewModel : ObservableObject
    {
        private readonly DatabaseService _databaseService;
        private readonly PacketSnifferService _snifferService;

        // UI 스레드에서 동작하는 타이머 (경과시간 업데이트 및 PID 상태 폴링)    
        private readonly DispatcherTimer _dispatcherTimer;

        // 사냥 시작 시간
        private DateTime _startTime;
        private readonly Stopwatch _stopwatch = new Stopwatch();

        // 현재 세션 ID
        private int _sessionId;

        // 타겟 프로세스 (현재 탭의 클라이언트 설치 경로 기반)
        private int _targetPid = -1;
        private string _clientPath;

        // [핵심] 다른 탭에서 이미 선점한 PID를 기억하여 겹치지 않게 방지 (동일 폴더 다클라 완벽 지원)
        private static readonly ConcurrentDictionary<int, bool> ClaimedPids = new();

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

        public HuntingViewModel(Monster monster, DatabaseService databaseService, PacketSnifferService snifferService, string clientPath)
        {
            CurrentMonster = monster;
            _databaseService = databaseService;
            _snifferService = snifferService;
            _clientPath = clientPath;

            FindTargetPid();

            // PacketSnifferService 이벤트 구독
            _snifferService.ItemDropped += OnItemDropped;
            _snifferService.StatusLog += (message) =>
            {
                AddStatusLog(message);
            };

            // DispatcherTimer 설정 (1초마다 경과시간 갱신 및 PID 폴링)
            _dispatcherTimer = new DispatcherTimer();
            _dispatcherTimer.Interval = TimeSpan.FromSeconds(1);
            _dispatcherTimer.Tick += OnTimerTick;
        }

        private void FindTargetPid()
        {
            if (string.IsNullOrEmpty(_clientPath) || _targetPid != -1) return;

            string expectedExePath = System.IO.Path.Combine(_clientPath, "gersang.exe").Replace("/", "\\");

            var processes = Process.GetProcessesByName("gersang");
            foreach (var p in processes)
            {
                // 다른 탭에서 이미 선점한 PID라면 건너뜀 (다클라 겹침 완벽 방지)
                if (ClaimedPids.ContainsKey(p.Id))
                    continue;

                bool isMatch = false;
                try
                {
                    string? pPath = p.MainModule?.FileName?.Replace("/", "\\");
                    if (string.Equals(pPath, expectedExePath, StringComparison.OrdinalIgnoreCase))
                    {
                        isMatch = true; 
                    }
                }
                catch
                {
                    isMatch = true;
                }

                if (isMatch)
                {
                    ClaimedPids.TryAdd(p.Id, true);
                    _targetPid = p.Id;
                    AddStatusLog($"[클라이언트 매칭 성공] 추적 PID: {_targetPid}");
                    break;
                }
            }

            if (_targetPid == -1)
            {
                AddStatusLog($"[대기 중] 게임 실행 및 런처 접속을 대기중입니다..");
            }
        }

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

        public async Task StartAsync()
        {
            _startTime = DateTime.Now;
            _sessionId = await _databaseService.AddSessionAsync(CurrentMonster.Id, _startTime);

            _stopwatch.Start();
            _dispatcherTimer.Start();
            _snifferService.Start();

            AddStatusLog("[사냥 기록 시작]");
        }

        private void OnTimerTick(object? sender, EventArgs e)
        {
            ElapsedTime = _stopwatch.Elapsed.ToString(@"hh\:mm\:ss");

            // 1초마다 거상이 켜졌는지, 혹은 꺼졌는지 모니터링
            if (_targetPid == -1)
            {
                FindTargetPid();
            }
            else
            {
                try
                {
                    var p = Process.GetProcessById(_targetPid);
                    if (p == null || p.HasExited)
                    {
                        ClaimedPids.TryRemove(_targetPid, out _);
                        _targetPid = -1;
                        AddStatusLog("[클라이언트 종료 감지] 클라이언트 재실행을 대기합니다.");
                    }
                }
                catch
                {
                    // 예외 발생 시 프로세스가 죽은 것으로 간주
                    ClaimedPids.TryRemove(_targetPid, out _);
                    _targetPid = -1;
                }
            }
        }

        private async void OnItemDropped(object? sender, DroppedItemEventArgs e)
        {
            // 아직 PID를 못 찾았거나, 다른 클라이언트(PID)의 드랍 정보라면 완전히 무시!
            if (_targetPid == -1 || e.Pid != _targetPid)
                return;

            await _databaseService.AddDropLogAsync(_sessionId, e.ItemName, e.Quantity);

            App.Current.Dispatcher.Invoke(() =>
            {
                DropLogs.Insert(0, new DropLog
                {
                    ItemName = e.ItemName,
                    Quantity = e.Quantity,
                    DroppedAt = e.DroppedAt
                });

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

        public async Task<int> StopAsync()
        {
            _dispatcherTimer.Stop();
            _stopwatch.Stop();

            // 추적 중이던 PID 반환 (다른 세션에서 쓸 수 있도록)
            if (_targetPid != -1)
            {
                ClaimedPids.TryRemove(_targetPid, out _);
                _targetPid = -1;
            }

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

    public partial class ItemSummary : ObservableObject
    {
        public string ItemName { get; set; } = string.Empty;

        [ObservableProperty]
        private int _totalQuantity;
    }
}