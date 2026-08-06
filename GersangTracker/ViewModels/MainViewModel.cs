using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GersangTracker.Models;
using GersangTracker.Services;
using GersangTracker.Views;
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

        // [게임 시작] 버튼용 스터브(뼈대) 커맨드
        [RelayCommand]
        private void StartGame(ClientTabViewModel tab)
        {
            if (tab == null) return;
            // TODO: 나중에 실제 클라이언트 프로세스 실행 로직으로 채워질 곳
            MessageBox.Show($"[{tab.Header}] 클라이언트 실행 준비 중...", "게임 시작 알림", MessageBoxButton.OK, MessageBoxImage.Information);
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
                    // 삭제된 계정이라면 탭 목록에서 제거
                    Tabs.Remove(tab);
                    if (SelectedTab == tab)
                    {
                        SelectedTab = Tabs.FirstOrDefault();
                    }
                }
                else
                {
                    // 단순 설정 변경이라면 이름(헤더) 등 새로고침
                    tab.RefreshHeader();
                }
            }
        }
    }
}