using GersangTracker.Models;
using OfficeOpenXml;
using OfficeOpenXml.Style;
using System.Drawing;
using System.IO;

namespace GersangTracker.Services
{
    public class ExcelService
    {
        public ExcelService()
        {
            // 비상업용 라이선스 설정
            ExcelPackage.License.SetNonCommercialPersonal("GersangTracker");

        }

        public void Export(Session session, Monster monster, List<DropLog> dropLogs, string filePath)
        {
            using var package = new ExcelPackage();

            // dropLogs 파라미터 추가
            CreateSummarySheet(package, session, monster, dropLogs);
            CreateDropLogSheet(package, dropLogs);
            CreateItemSummarySheet(package, dropLogs);

            package.SaveAs(new FileInfo(filePath));
        }

        // 시트1 - 요약
        private void CreateSummarySheet(ExcelPackage package, Session session, Monster monster, List<DropLog> dropLogs)
        {
            var ws = package.Workbook.Worksheets.Add("요약");

            // 헤더 스타일
            SetHeader(ws, 1, 1, "항목");
            SetHeader(ws, 1, 2, "내용");

            // 기본 정보
            TimeSpan huntingTime = session.EndedAt - session.StartedAt;
            double hours = huntingTime.TotalHours;
            long profitPerHour = hours > 0 ? (long)(session.TotalProfit / hours) : 0;

            ws.Cells[2, 1].Value = "날짜";
            ws.Cells[2, 2].Value = session.StartedAt.ToString("yyyy-MM-dd");

            ws.Cells[3, 1].Value = "몬스터";
            ws.Cells[3, 2].Value = monster.Name;

            ws.Cells[4, 1].Value = "사냥 시작";
            ws.Cells[4, 2].Value = session.StartedAt.ToString("HH:mm:ss");

            ws.Cells[5, 1].Value = "사냥 종료";
            ws.Cells[5, 2].Value = session.EndedAt.ToString("HH:mm:ss");

            ws.Cells[6, 1].Value = "사냥 시간";
            ws.Cells[6, 2].Value = $"{(int)huntingTime.TotalHours}시간 {huntingTime.Minutes}분 {huntingTime.Seconds}초";

            ws.Cells[7, 1].Value = "총 수익";
            ws.Cells[7, 2].Value = session.TotalProfit;
            ws.Cells[7, 2].Style.Numberformat.Format = "#,##0";

            ws.Cells[8, 1].Value = "시간당 수익";
            ws.Cells[8, 2].Value = profitPerHour;
            ws.Cells[8, 2].Style.Numberformat.Format = "#,##0";

            // 빈 줄
            // 아이템 목록 헤더
            SetHeader(ws, 10, 1, "아이템명");
            SetHeader(ws, 10, 2, "수량");
            SetHeader(ws, 10, 3, "단가");
            SetHeader(ws, 10, 4, "합계");

            // 아이템 합산
            var grouped = dropLogs
                .GroupBy(d => d.ItemName)
                .Select(g => new
                {
                    ItemName = g.Key,
                    TotalQuantity = g.Sum(d => d.Quantity),
                    UnitPrice = g.First().UnitPrice,
                    Total = g.Sum(d => d.UnitPrice * d.Quantity)
                })
                .ToList();

            for (int i = 0; i < grouped.Count; i++)
            {
                int row = i + 11;
                var item = grouped[i];

                ws.Cells[row, 1].Value = item.ItemName;
                ws.Cells[row, 2].Value = item.TotalQuantity;
                ws.Cells[row, 3].Value = item.UnitPrice;
                ws.Cells[row, 4].Value = item.Total;

                ws.Cells[row, 3].Style.Numberformat.Format = "#,##0";
                ws.Cells[row, 4].Style.Numberformat.Format = "#,##0";
            }

            ws.Column(1).Width = 15;
            ws.Column(2).Width = 20;
            ws.Column(3).Width = 15;
            ws.Column(4).Width = 15;
        }

        // 시트2 - 드랍 상세 로그
        private void CreateDropLogSheet(ExcelPackage package, List<DropLog> dropLogs)
        {
            var ws = package.Workbook.Worksheets.Add("드랍 상세 로그");

            // 헤더
            SetHeader(ws, 1, 1, "시간");
            SetHeader(ws, 1, 2, "아이템명");
            SetHeader(ws, 1, 3, "수량");
            SetHeader(ws, 1, 4, "단가");
            SetHeader(ws, 1, 5, "합계");

            // 데이터
            for (int i = 0; i < dropLogs.Count; i++)
            {
                int row = i + 2;
                var log = dropLogs[i];
                long total = log.UnitPrice * log.Quantity;

                ws.Cells[row, 1].Value = log.DroppedAt.ToString("HH:mm:ss");
                ws.Cells[row, 2].Value = log.ItemName;
                ws.Cells[row, 3].Value = log.Quantity;
                ws.Cells[row, 4].Value = log.UnitPrice;
                ws.Cells[row, 5].Value = total;

                ws.Cells[row, 4].Style.Numberformat.Format = "#,##0";
                ws.Cells[row, 5].Style.Numberformat.Format = "#,##0";
            }

            ws.Column(1).Width = 12;
            ws.Column(2).Width = 25;
            ws.Column(3).Width = 8;
            ws.Column(4).Width = 15;
            ws.Column(5).Width = 15;
        }

        // 시트3 - 아이템 합산
        private void CreateItemSummarySheet(ExcelPackage package, List<DropLog> dropLogs)
        {
            var ws = package.Workbook.Worksheets.Add("아이템 합산");

            // 헤더
            SetHeader(ws, 1, 1, "아이템명");
            SetHeader(ws, 1, 2, "총 수량");
            SetHeader(ws, 1, 3, "단가");
            SetHeader(ws, 1, 4, "합계");

            // 아이템별 합산
            var grouped = dropLogs
                .GroupBy(d => d.ItemName)
                .Select(g => new
                {
                    ItemName = g.Key,
                    TotalQuantity = g.Sum(d => d.Quantity),
                    UnitPrice = g.First().UnitPrice,
                    Total = g.Sum(d => d.UnitPrice * d.Quantity)
                })
                .ToList();

            for (int i = 0; i < grouped.Count; i++)
            {
                int row = i + 2;
                var item = grouped[i];

                ws.Cells[row, 1].Value = item.ItemName;
                ws.Cells[row, 2].Value = item.TotalQuantity;
                ws.Cells[row, 3].Value = item.UnitPrice;
                ws.Cells[row, 4].Value = item.Total;

                ws.Cells[row, 3].Style.Numberformat.Format = "#,##0";
                ws.Cells[row, 4].Style.Numberformat.Format = "#,##0";
            }

            ws.Column(1).Width = 25;
            ws.Column(2).Width = 10;
            ws.Column(3).Width = 15;
            ws.Column(4).Width = 15;
        }

        // 헤더 셀 스타일 설정
        private void SetHeader(ExcelWorksheet ws, int row, int col, string title)
        {
            ws.Cells[row, col].Value = title;
            ws.Cells[row, col].Style.Font.Bold = true;
            ws.Cells[row, col].Style.Fill.PatternType = ExcelFillStyle.Solid;
            ws.Cells[row, col].Style.Fill.BackgroundColor.SetColor(Color.LightGray);
        }
    }
}