using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GersangTracker.Models;
using GersangTracker.Services;
using System.Collections.ObjectModel;
using System.Text.RegularExpressions;

namespace GersangTracker.ViewModels
{
    public partial class PriceItem : ObservableObject
    {
        public string ItemName { get; set; } = string.Empty;

        [ObservableProperty]
        private int _totalQuantity;

        [ObservableProperty]
        private string _unitPriceInput = string.Empty;

        public long UnitPrice =>
            long.TryParse(UnitPriceInput.Replace(",", ""), out long price) ? price : 0;

        public long Total => UnitPrice * TotalQuantity;
    }

    public partial class PriceViewModel : ObservableObject
    {
        private readonly DatabaseService _databaseService;
        private readonly int _sessionId;
        private readonly Monster _monster;

        public ObservableCollection<PriceItem> PriceItems { get; } = new();

        public string MonsterName => _monster.Name;
        public Monster Monster => _monster;

        [ObservableProperty]
        private string _newItemName = string.Empty;

        [ObservableProperty]
        private string _newItemQuantity = "1";

        public PriceViewModel(int sessionId, Monster monster, List<ItemSummary> itemSummaries, DatabaseService databaseService)
        {
            _databaseService = databaseService;
            _sessionId = sessionId;
            _monster = monster;

            foreach (var item in itemSummaries)
            {
                PriceItems.Add(new PriceItem
                {
                    ItemName = item.ItemName,
                    TotalQuantity = item.TotalQuantity
                });
            }
        }

        public async Task LoadPreviousPricesAsync()
        {
            var savedPrices = await _databaseService.GetItemPricesByMonsterAsync(_monster.Id);

            foreach (var priceItem in PriceItems)
            {
                var saved = savedPrices.FirstOrDefault(p => p.ItemName == priceItem.ItemName);
                if (saved != null)
                    priceItem.UnitPriceInput = saved.UnitPrice.ToString("N0");
            }
        }

        [RelayCommand]
        private void AddItem()
        {
            var name = NewItemName.Trim();
            if (string.IsNullOrEmpty(name)) return;
            if (PriceItems.Any(x => x.ItemName == name)) return;

            int.TryParse(NewItemQuantity, out int qty);
            if (qty <= 0) qty = 1;

            PriceItems.Add(new PriceItem
            {
                ItemName = name,
                TotalQuantity = qty,
                UnitPriceInput = "0"
            });

            NewItemName = string.Empty;
            NewItemQuantity = "1";
        }

        [RelayCommand]
        private void RemoveItem(PriceItem item)
        {
            PriceItems.Remove(item);
        }

        [RelayCommand]
        private async Task CalculateAsync()
        {
            foreach (var item in PriceItems)
            {
                if (item.UnitPrice > 0)
                    await _databaseService.SaveItemPriceAsync(_monster.Id, item.ItemName, item.UnitPrice);
            }

            var syncItems = PriceItems.Select(p => new PriceItemSummary
            {
                ItemName = p.ItemName,
                TotalQuantity = p.TotalQuantity,
                UnitPrice = p.UnitPrice
            }).ToList();

            await _databaseService.SyncDropLogsAsync(_sessionId, syncItems);

            long totalProfit = PriceItems.Sum(p => p.Total);
            await _databaseService.UpdateSessionProfitAsync(_sessionId, totalProfit);
        }

        public long TotalProfit => PriceItems.Sum(p => p.Total);

        public async Task<Session> GetSessionAsync()
        {
            var session = await _databaseService.GetSessionsByMonsterAsync(_monster.Id);
            return session.First(s => s.Id == _sessionId);
        }

        public async Task<List<DropLog>> GetDropLogsAsync()
        {
            return await _databaseService.GetDropLogsBySessionAsync(_sessionId);
        }
    }
}