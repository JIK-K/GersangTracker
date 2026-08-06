using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GersangTracker.Models;
using GersangTracker.Services;
using GersangTracker.Views;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;

namespace GersangTracker.ViewModels
{
    public partial class ClientTabViewModel : ObservableObject
    {
        private readonly DatabaseService _databaseService;

        public Account Account { get; }

        // 탭 헤더에 표시할 이름
        public string Header => string.IsNullOrEmpty(Account.UserId) ? "New Account" : Account.UserId;

        public ObservableCollection<Monster> Monsters { get; } = new();

        [ObservableProperty]
        private Monster? _selectedMonster;

        [ObservableProperty]
        private string _newMonsterName = string.Empty;

        public ClientTabViewModel(Account account, DatabaseService databaseService)
        {
            Account = account;
            _databaseService = databaseService;

            _ = LoadMonstersAsync();
        }

        public void RefreshHeader() => OnPropertyChanged(nameof(Header));

        [RelayCommand]
        private async Task LoadMonstersAsync()
        {
            // 계정별 고유 몬스터 리스트 로드
            var monsters = await _databaseService.GetMonstersByAccountIdAsync(Account.Id);
            Monsters.Clear();
            foreach (var monster in monsters)
                Monsters.Add(monster);
        }

        [RelayCommand]
        private async Task AddMonsterAsync()
        {
            if (string.IsNullOrEmpty(NewMonsterName)) return;
            try
            {
                // 추가 시 현재 탭의 Account.Id 주입
                await _databaseService.AddMonsterAsync(Account.Id, NewMonsterName);
                NewMonsterName = string.Empty;
                await LoadMonstersAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "오류", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        [RelayCommand]
        private async Task RenameMonsterAsync(Monster monster)
        {
            if (string.IsNullOrEmpty(monster.Name)) return;
            var dialog = new RenameDialog(monster.Name) { Owner = Application.Current.MainWindow };
            if (dialog.ShowDialog() == true)
            {
                await _databaseService.UpdateMonsterNameAsync(monster.Id, dialog.NewName);
                await LoadMonstersAsync();
            }
        }

        [RelayCommand]
        private async Task DeleteMonsterAsync(Monster monster)
        {
            await _databaseService.DeleteMonsterAsync(monster.Id);
            await LoadMonstersAsync();
        }

        [RelayCommand]
        private void StartHunting(Monster monster)
        {
            var snifferService = App.ServiceProvider.GetRequiredService<PacketSnifferService>();

            // 수정됨: Account가 아닌 ClientInstance 테이블에 있는 설치 경로를 가져옴
            var clientPath = Account.ClientInstance?.InstallPath ?? string.Empty;

            if (string.IsNullOrEmpty(clientPath))
            {
                MessageBox.Show("클라이언트 설치 경로가 설정되지 않았습니다. 우측 톱니바퀴에서 먼저 설정해주세요.", "알림", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var viewModel = new HuntingViewModel(monster, _databaseService, snifferService, clientPath);
            new HuntingWindow(viewModel).Show();
        }

        [RelayCommand]
        private void OpenSessions(Monster monster)
        {
            var viewModel = new SessionViewModel(monster, _databaseService);
            new SessionWindow(viewModel).Show();
        }
    }
}