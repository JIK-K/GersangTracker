using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GersangTracker.Models;
using GersangTracker.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using System.Text.RegularExpressions;

namespace GersangTracker.ViewModels
{
    // ViewModels/MonsterItemViewModel.cs
    public partial class MonsterItemViewModel : ObservableObject
    {
        private readonly DatabaseService _db;
        private readonly int _monsterId;

        [ObservableProperty]
        private string _monsterName = string.Empty;

        [ObservableProperty]
        private string _newItemName = string.Empty;

        public ObservableCollection<MonsterItem> Items { get; } = new();

        public MonsterItemViewModel(DatabaseService db, int monsterId, string monsterName)
        {
            _db = db;
            _monsterId = monsterId;
            MonsterName = monsterName;
        }

        public async Task LoadAsync()
        {
            var list = await _db.GetMonsterItemsAsync(_monsterId);
            Items.Clear();
            foreach (var item in list) Items.Add(item);
        }

        [RelayCommand]
        private async Task AddItem()
        {
            var name = NewItemName.Trim();
            if (string.IsNullOrEmpty(name)) return;
            name = Regex.Replace(name, @"[^가-힣]", "");
            if (string.IsNullOrEmpty(name)) return;
            if (Items.Any(x => x.ItemName == name)) return;

            await _db.AddMonsterItemAsync(_monsterId, name);
            NewItemName = string.Empty;
            await LoadAsync();
        }

        [RelayCommand]
        private async Task DeleteItem(MonsterItem item)
        {
            await _db.DeleteMonsterItemAsync(item.Id);
            await LoadAsync();
        }
    }
}
