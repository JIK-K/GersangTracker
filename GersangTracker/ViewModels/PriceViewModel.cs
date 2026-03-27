using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GersangTracker.Models;
using GersangTracker.Services;
using System.Collections.ObjectModel;

namespace GersangTracker.ViewModels
{
    // 가격 입력 화면 표시용 임시 모델
    public partial class PriceItem : ObservableObject
    {
        public string ItemName { get; set; } = string.Empty;
        public int TotalQuantity { get; set; }

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

        public PriceViewModel(int sessionId, Monster monster, List<ItemSummary> itemSummaries)
        {
            _databaseService = new DatabaseService();
            _sessionId = sessionId;
            _monster = monster;

            // ItemSummary → PriceItem 변환
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

        // 계산하기
        [RelayCommand]
        private async Task CalculateAsync()
        {
            // 단가 DB 저장
            foreach (var item in PriceItems)
            {
                if (item.UnitPrice > 0)
                    await _databaseService.SaveItemPriceAsync(_monster.Id, item.ItemName, item.UnitPrice);
            }

            // 드랍 로그 단가 업데이트
            var dropLogs = await _databaseService.GetDropLogsBySessionAsync(_sessionId);
            foreach (var log in dropLogs)
            {
                var priceItem = PriceItems.FirstOrDefault(p => p.ItemName == log.ItemName);
                if (priceItem != null)
                    log.UnitPrice = priceItem.UnitPrice;
            }

            // 총 수익 계산
            long totalProfit = PriceItems.Sum(p => p.Total);

            // 세션 수익 업데이트
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