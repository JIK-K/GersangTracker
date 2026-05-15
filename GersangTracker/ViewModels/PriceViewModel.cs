using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GersangTracker.Models;
using GersangTracker.Services;
using System.Collections.ObjectModel;
using System.Text.RegularExpressions;

namespace GersangTracker.ViewModels
{
    // 가격 입력 화면 표시용 임시 모델
    public partial class PriceItem : ObservableObject
    {
        public string ItemName { get; set; } = string.Empty;

        // 수량 - 편집 가능하도록 변경
        [ObservableProperty]
        private int _totalQuantity;

        // 단가 입력값 - 변경시 UI 자동 반영
        [ObservableProperty]
        private string _unitPriceInput = string.Empty;

        // 입력된 단가 (숫자 변환)
        public long UnitPrice =>
            long.TryParse(UnitPriceInput.Replace(",", ""), out long price) ? price : 0;

        // 합계
        public long Total => UnitPrice * TotalQuantity;
    }

    public partial class PriceViewModel : ObservableObject
    {
        private readonly DatabaseService _databaseService;
        private readonly int _sessionId;
        private readonly Monster _monster;

        // 가격 입력 목록
        public ObservableCollection<PriceItem> PriceItems { get; } = new();

        // 몬스터명 표시
        public string MonsterName => _monster.Name;
        public Monster Monster => _monster;

        // 아이템 직접 추가용
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

        // 이전 세션 단가 자동 불러오기
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

        // 아이템 직접 추가
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

        // 아이템 삭제
        [RelayCommand]
        private void RemoveItem(PriceItem item)
        {
            PriceItems.Remove(item);
        }

        // 계산하기
        [RelayCommand]
        private async Task CalculateAsync()
        {
            // 1. 전역 아이템 단가 저장 (몬스터별 설정 유지용)
            foreach (var item in PriceItems)
            {
                if (item.UnitPrice > 0)
                    await _databaseService.SaveItemPriceAsync(_monster.Id, item.ItemName, item.UnitPrice);
            }

            // 2. 현재 세션의 드랍 로그 동기화 (수량, 단가, 신규 아이템)
            var syncItems = PriceItems.Select(p => new PriceItemSummary
            {
                ItemName = p.ItemName,
                TotalQuantity = p.TotalQuantity,
                UnitPrice = p.UnitPrice
            }).ToList();

            await _databaseService.SyncDropLogsAsync(_sessionId, syncItems);

            // 3. 세션 정보 업데이트 (총 수익 및 종료 시간)
            long totalProfit = PriceItems.Sum(p => p.Total);
            await _databaseService.UpdateSessionAsync(_sessionId, DateTime.Now, totalProfit);
        }


        // 총 수익
        public long TotalProfit => PriceItems.Sum(p => p.Total);

        // 세션 조회
        public async Task<Session> GetSessionAsync()
        {
            var session = await _databaseService.GetSessionsByMonsterAsync(_monster.Id);
            return session.First(s => s.Id == _sessionId);
        }

        // 드랍 로그 조회
        public async Task<List<DropLog>> GetDropLogsAsync()
        {
            return await _databaseService.GetDropLogsBySessionAsync(_sessionId);
        }
    }
}