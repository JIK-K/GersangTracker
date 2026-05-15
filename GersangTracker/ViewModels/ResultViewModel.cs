using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GersangTracker.Models;
using GersangTracker.Services;

namespace GersangTracker.ViewModels
{
    public partial class ResultViewModel : ObservableObject
    {
        private readonly DatabaseService _databaseService;
        private readonly ExcelService _excelService;
        private readonly Session _session;
        private readonly Monster _monster;
        private readonly List<DropLog> _dropLogs;

        // 몬스터명
        public string MonsterName => _monster.Name;

        // 아이템 합산 목록 (결과 표시용)
        public List<PriceItem> PriceItems { get; }

        // 총 수익
        public long TotalProfit => _session.TotalProfit;

        // 사냥 시간
        public string HuntingTime
        {
            get
            {
                var elapsed = _session.EndedAt - _session.StartedAt;
                return $"{(int)elapsed.TotalHours}시간 {elapsed.Minutes}분 {elapsed.Seconds}초";
            }
        }

        // 시간당 수익
        public long ProfitPerHour
        {
            get
            {
                double hours = (_session.EndedAt - _session.StartedAt).TotalHours;
                return hours > 0 ? (long)(_session.TotalProfit / hours) : 0;
            }
        }

        public ResultViewModel(Session session, Monster monster, List<PriceItem> priceItems, List<DropLog> dropLogs, DatabaseService databaseService, ExcelService excelService)
        {
            _databaseService = databaseService;
            _excelService = excelService;
            _session = session;
            _monster = monster;
            _dropLogs = dropLogs;
            PriceItems = priceItems;
        }

        // 엑셀 내보내기
        [RelayCommand]
        private void ExportExcel()
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Title = "엑셀 파일 저장",
                Filter = "Excel 파일 (*.xlsx)|*.xlsx",
                FileName = $"{_monster.Name}_{_session.StartedAt:yyyyMMdd_HHmmss}"
            };

            if (dialog.ShowDialog() == true)
            {
                _excelService.Export(_session, _monster, _dropLogs, dialog.FileName);
            }
        }
    }
}