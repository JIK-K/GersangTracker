using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GersangTracker.Models;
using GersangTracker.Services;
using GersangTracker.Views;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;
using System.Linq;

namespace GersangTracker.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private readonly DatabaseService _databaseService;

        public ObservableCollection<ClientTabViewModel> Tabs { get; } = new();

        [ObservableProperty]
        private ClientTabViewModel? _selectedTab;

        public MainViewModel(DatabaseService databaseService)
        {
            _databaseService = databaseService;
            _ = LoadTabsAsync();
        }

        private async Task LoadTabsAsync()
        {
            var accounts = await _databaseService.GetAccountsAsync();
            Tabs.Clear();
            foreach (var acc in accounts)
            {
                Tabs.Add(new ClientTabViewModel(acc, _databaseService));
            }

            if (Tabs.Any())
            {
                SelectedTab = Tabs.First();
            }
        }

        // 게임 시작 커맨드 (실제 클라이언트 실행, 초고속 패치 적용 및 Run.exe 완전 우회)
        [RelayCommand]
        private async Task StartGame(ClientTabViewModel tab)
        {
            if (tab == null || tab.Account == null) return;

            string installPath = tab.Account.ClientInstance?.InstallPath ?? string.Empty;
            if (string.IsNullOrWhiteSpace(installPath))
            {
                MessageBox.Show("클라이언트 설치 경로가 설정되어 있지 않습니다.\n우측 톱니바퀴 버튼을 눌러 경로를 설정해주세요.", "알림", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            GameServer server = GameServer.Korea_Live; // 임시로 본섭 고정 (추후 옵션 연동 가능)
            var patchManager = new PatchManager();

            // 1. 클라이언트와 서버의 버전 비교
            int? currentVersion = patchManager.GetCurrentClientVersion(installPath);
            int? latestVersion = await PatchReadmeHelper.GetLatestVersionAsync(server);

            if (latestVersion.HasValue && (currentVersion == null || currentVersion.Value < latestVersion.Value))
            {
                var result = MessageBox.Show(
                    currentVersion == null ? "거상 클라이언트가 설치되어 있지 않습니다.\n초고속 전체 설치를 진행하시겠습니까?" : $"새로운 업데이트(v{latestVersion.Value})가 있습니다.\n초고속 패치를 진행하시겠습니까?",
                    "업데이트 확인", MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    var patchWindow = new PatchWindow
                    {
                        Owner = Application.Current.MainWindow
                    };

                    // 창을 띄우면서 패치(또는 전체 설치) 시작!
                    if (patchWindow.DataContext is PatchViewModel patchVM)
                    {
                        patchVM.StartPatch(server, currentVersion, latestVersion.Value, installPath);
                    }

                    patchWindow.ShowDialog(); // 패치가 끝날 때까지 대기

                    // 2. 패치 완료 후 버전 다시 확인 (취소했거나 실패했다면 버전이 낮을 것임)
                    currentVersion = patchManager.GetCurrentClientVersion(installPath);
                    if (currentVersion == null || currentVersion.Value < latestVersion.Value)
                    {
                        MessageBox.Show("패치가 완료되지 않아 게임을 실행할 수 없습니다.", "알림", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                }
                else
                {
                    return; // 패치를 거부하면 게임 실행 취소
                }
            }

            // 3. 패치가 정상적으로 완료되었다면 런처(Run.exe)를 찾음
            string runExePath = System.IO.Path.Combine(installPath, "Run.exe");
            if (!System.IO.File.Exists(runExePath))
            {
                MessageBox.Show($"해당 경로에 거상 런처(Run.exe)가 존재하지 않습니다.\n설정하신 경로:\n{installPath}", "경로 오류", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // 4. 로그인 및 다이렉트 게임 실행
            string userId = tab.Account.UserId ?? string.Empty;
            string plainPw = SecureDataHelper.Decrypt(tab.Account.EncryptedPassword ?? string.Empty);
            if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(plainPw))
            {
                MessageBox.Show("계정 정보가 없습니다.", "알림", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                var authService = new GersangAuthService();

                // 🔥 백그라운드 로그인 후 토큰(CmdStr) 획득
                string cmdStr = await authService.GetGameStartTokenAsync(userId, plainPw);

                // 🔥 획득한 토큰을 런처(Run.exe)의 파라미터로 넘겨 관리자 권한으로 실행
                var processInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = runExePath,
                    Arguments = cmdStr,
                    WorkingDirectory = installPath,
                    UseShellExecute = true,
                    Verb = "runas"
                };

                System.Diagnostics.Process.Start(processInfo);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"오류 발생: {ex.Message}", "게임 시작 실패", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private void AddNewTab()
        {
            var newAccount = new Account();
            var settingsViewModel = new AccountSettingsViewModel(newAccount, _databaseService);
            var window = new AccountSettingsWindow(settingsViewModel)
            {
                Owner = Application.Current.MainWindow
            };

            if (window.ShowDialog() == true && !settingsViewModel.IsDeleted)
            {
                var newTab = new ClientTabViewModel(newAccount, _databaseService);
                Tabs.Add(newTab);
                SelectedTab = newTab;
            }
        }

        [RelayCommand]
        private void OpenAccountSettings(ClientTabViewModel tab)
        {
            if (tab == null) return;

            var viewModel = new AccountSettingsViewModel(tab.Account, _databaseService);
            var window = new AccountSettingsWindow(viewModel)
            {
                Owner = Application.Current.MainWindow
            };

            if (window.ShowDialog() == true)
            {
                if (viewModel.IsDeleted)
                {
                    Tabs.Remove(tab);
                    if (SelectedTab == tab)
                    {
                        SelectedTab = Tabs.FirstOrDefault();
                    }
                }
                else
                {
                    tab.RefreshHeader();
                }
            }
        }
    }
}