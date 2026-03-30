using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GersangTracker.Models;
using GersangTracker.Services;
using GersangTracker.Views;
using System.Collections.ObjectModel;
using System.Windows;

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

        public MainViewModel()
        {
            _databaseService = new DatabaseService();
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

        // 2번 - 사냥 시작 전 아이템 등록 여부 체크
        [RelayCommand]
        private async Task StartHunting(Monster monster)
        {
            var items = await _databaseService.GetMonsterItemsAsync(monster.Id);
            if (items.Count == 0)
            {
                MessageBox.Show(
                    "드랍 아이템이 등록되어 있지 않습니다.\n아이템 관리에서 아이템을 먼저 추가해주세요.",
                    "아이템 없음",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            var viewModel = new HuntingViewModel(monster);
            var window = new HuntingWindow(viewModel);
            // 4번 - Owner 제거해서 창 독립
            window.Show();
        }

        [RelayCommand]
        private void OpenSessions(Monster monster)
        {
            var viewModel = new SessionViewModel(monster);
            var window = new SessionWindow(viewModel);
            // 4번 - Owner 제거해서 창 독립
            window.Show();
        }

        [RelayCommand]
        private async Task ManageItems(Monster monster)
        {
            var vm = new MonsterItemViewModel(_databaseService, monster.Id, monster.Name);
            var dialog = new MonsterItemDialog(vm)
            {
                Owner = Application.Current.MainWindow
            };
            dialog.ShowDialog();
        }
    }
}