using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GersangTracker.Models;
using GersangTracker.Services;
using GersangTracker.Views;
using System.Collections.ObjectModel;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;

namespace GersangTracker.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private readonly DatabaseService _databaseService;

        public ObservableCollection<Monster> Monsters { get; } = new();

        [ObservableProperty]
        private Monster? _selectedMonster;

        [ObservableProperty]
        private string _newMonsterName = string.Empty;

        public MainViewModel(DatabaseService databaseService)
        {
            _databaseService = databaseService;
            // 앱 실행 시 목록 자동 로드
            _ = LoadMonstersAsync();
        }

        [RelayCommand]
        private async Task LoadMonstersAsync()
        {
            var monsters = await _databaseService.GetMonstersAsync();
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
                await _databaseService.AddMonsterAsync(NewMonsterName);
                NewMonsterName = string.Empty;
                await LoadMonstersAsync();
            }
            catch (InvalidOperationException ex)
            {
                MessageBox.Show(ex.Message, "중복 오류", MessageBoxButton.OK, MessageBoxImage.Warning);
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
            var dialog = new RenameDialog(monster.Name);
            dialog.Owner = Application.Current.MainWindow;
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

        // 사냥 시작
        [RelayCommand]
        private async Task StartHunting(Monster monster)
        {

            var snifferService = App.ServiceProvider.GetRequiredService<PacketSnifferService>();
            var viewModel = new HuntingViewModel(monster, _databaseService, snifferService);
            var window = new HuntingWindow(viewModel);
            // 4번 - Owner 제거해서 창 독립
            window.Show();
        }

        [RelayCommand]
        private void OpenSessions(Monster monster)
        {
            var viewModel = new SessionViewModel(monster, _databaseService);
            var window = new SessionWindow(viewModel);
            // 4번 - Owner 제거해서 창 독립
            window.Show();
        }
    }
}