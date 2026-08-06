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

        // 게임 시작 커맨드 (실제 클라이언트 실행)
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

            string runExePath = System.IO.Path.Combine(installPath, "Run.exe");
            if (!System.IO.File.Exists(runExePath))
            {
                MessageBox.Show($"해당 경로에 거상 런처(Run.exe)가 존재하지 않습니다.\n설정하신 경로:\n{installPath}", "경로 오류", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string userId = tab.Account.UserId ?? string.Empty;
            // 암호화된 비밀번호를 복호화해서 통신에 사용
            string plainPw = SecureDataHelper.Decrypt(tab.Account.EncryptedPassword ?? string.Empty);

            if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(plainPw))
            {
                MessageBox.Show("계정 아이디 또는 비밀번호가 입력되지 않았습니다.\n우측 톱니바퀴 버튼을 눌러 정보를 입력해주세요.", "알림", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                var authService = new GersangAuthService();

                // 1. 백그라운드 로그인 후 토큰 획득
                string cmdStr = await authService.GetGameStartTokenAsync(userId, plainPw);

                // 2. 획득한 토큰을 런처(Run.exe)의 파라미터로 넘겨 관리자 권한으로 실행
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
                MessageBox.Show($"게임 실행 중 오류가 발생했습니다.\n\n{ex.Message}", "게임 시작 실패", MessageBoxButton.OK, MessageBoxImage.Error);
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