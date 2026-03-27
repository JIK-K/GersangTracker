using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GersangTracker.Models;
using GersangTracker.Services;
using GersangTracker.Views;
using OfficeOpenXml;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using System.Windows;

namespace GersangTracker.ViewModels
{
    public partial class MainViewModel: ObservableObject
    {
        private readonly DatabaseService _databaseService;

        // 표시될 몬스터 카드 목록
        // [ObservableCollection] - View UI 자동 반영
        public ObservableCollection<Monster> Monsters { get; } = new();

        [ObservableProperty]
        private Monster? _selectedMonster;

        [ObservableProperty]
        private string _newMonsterName = string.Empty;

        public MainViewModel()
        {
            _databaseService = new DatabaseService();
        }

        // [RelayCommand] - ViewModel Method View Command Binding
        [RelayCommand]
        private async Task LoadMonstersAsync()
        {
            var monsters = await _databaseService.GetMonstersAsync();
            Monsters.Clear();
            foreach (var monster in monsters)
            {
                Monsters.Add(monster);
            }
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
            catch(InvalidOperationException ex)
            {
                MessageBox.Show(ex.Message, "중복 오류", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "중복 오류", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        [RelayCommand]
        private async Task RenameMonsterAsync(Monster monster)
        {
            if (string.IsNullOrEmpty(monster.Name)) return;
            var dialog = new RenameDialog(monster.Name);
            dialog.Owner = Application.Current.MainWindow;
            if(dialog.ShowDialog() == true)
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
            var viewModel = new HuntingViewModel(monster);
            var window = new HuntingWindow(viewModel);
            window.Owner = Application.Current.MainWindow;
            window.Show();
        }

        // 세션 열기
        [RelayCommand]
        private void OpenSessions(Monster monster)
        {
            var viewModel = new SessionViewModel(monster);
            var window = new SessionWindow(viewModel);
            window.Owner = Application.Current.MainWindow;
            window.Show();
        }
    }
}
