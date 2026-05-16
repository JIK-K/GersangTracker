using GersangTracker.Data;
using GersangTracker.Models;
using Microsoft.EntityFrameworkCore;

namespace GersangTracker.Services
{
    public class DatabaseService
    {
        // [Monster] - [Read] 몬스터 전체 조회
        public async Task<List<Monster>> GetMonstersAsync()
        {
            using var db = new AppDbContext();
            return await db.Monsters.ToListAsync();
        }

        // [Monster] - [Create] 몬스터 추가
        public async Task AddMonsterAsync(string name)
        {
            using var db = new AppDbContext();
            var isExist = await db.Monsters.AnyAsync(m => m.Name == name);
            if (isExist)
            {
                throw new InvalidOperationException("이미 존재하는 몬스터 이름입니다.");
            }
            db.Monsters.Add(new Monster { Name = name });
            await db.SaveChangesAsync();
        }

        // [Monster] - [Update] 몬스터 이름 수정
        public async Task UpdateMonsterNameAsync(int monsterId, string newName)
        {
            using var db = new AppDbContext();
            var monster = await db.Monsters.FindAsync(monsterId);
            if (monster == null) return;
            monster.Name = newName;
            await db.SaveChangesAsync();
        }

        // [Monster] - [Delete] 몬스터 삭제
        public async Task DeleteMonsterAsync(int monsterId)
        {
            using var db = new AppDbContext();
            var monster = await db.Monsters.FindAsync(monsterId);
            if (monster == null) return;
            db.Monsters.Remove(monster);
            await db.SaveChangesAsync();
        }

        // [Session] - [Create] 세션 추가
        public async Task<int> AddSessionAsync(int monsterId, DateTime startedAt)
        {
            using var db = new AppDbContext();
            var session = new Session
            {
                MonsterId = monsterId,
                StartedAt = startedAt,
                EndedAt = startedAt,
                TotalProfit = 0
            };
            db.Sessions.Add(session);
            await db.SaveChangesAsync();
            return session.Id;
        }

        // [Session] - [Update] 세션 종료 시간 및 수익 업데이트
        public async Task UpdateSessionAsync(int sessionId, DateTime endedAt, long totalProfit)
        {
            using var db = new AppDbContext();
            var session = await db.Sessions.FindAsync(sessionId);
            if (session == null) return;
            session.EndedAt = endedAt;
            session.TotalProfit = totalProfit;
            await db.SaveChangesAsync();
        }

        // [Session] - [Read] 몬스터별 세션 목록 조회
        public async Task<List<Session>> GetSessionsByMonsterAsync(int monsterId)
        {
            using var db = new AppDbContext();
            return await db.Sessions
                .Where(s => s.MonsterId == monsterId)
                .OrderByDescending(s => s.StartedAt)
                .ToListAsync();
        }

        // [Session] - [Delete] 세션 삭제
        public async Task DeleteSessionAsync(int sessionId)
        {
            using var db = new AppDbContext();
            var session = await db.Sessions.FindAsync(sessionId);
            if (session == null) return;
            db.Sessions.Remove(session);
            await db.SaveChangesAsync();
        }

        // [DropLog] - [Create] 드롭 로그 추가
        public async Task AddDropLogAsync(int sessionId, string itemName, int quantity)
        {
            try
            {
                using var db = new AppDbContext();
                db.DropLogs.Add(new DropLog
                {
                    SessionId = sessionId,
                    DroppedAt = DateTime.Now,
                    ItemName = itemName,
                    Quantity = quantity,
                    UnitPrice = 0
                });
                await db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"DB Error in AddDropLogAsync: {ex.Message}");
            }
        }

        // [DropLog] - [Read] 세션별 드롭 로그 조회
        public async Task<List<DropLog>> GetDropLogsBySessionAsync(int sessionId)
        {
            using var db = new AppDbContext();
            return await db.DropLogs
                .Where(d => d.SessionId == sessionId)
                .OrderBy(d => d.DroppedAt)
                .ToListAsync();
        }

        // [ItemPrice] - [Read] 몬스터별 아이템 단가 조회
        public async Task<List<ItemPrice>> GetItemPricesByMonsterAsync(int monsterId)
        {
            using var db = new AppDbContext();
            return await db.ItemPrices
                .Where(p => p.MonsterId == monsterId)
                .ToListAsync();
        }

        // [ItemPrice] - [Create/Update] 아이템 단가 저장 (없으면 추가, 있으면 수정)
        public async Task SaveItemPriceAsync(int monsterId, string itemName, long unitPrice)
        {
            using var db = new AppDbContext();
            var existing = await db.ItemPrices
                .FirstOrDefaultAsync(p => p.MonsterId == monsterId && p.ItemName == itemName);

            if (existing != null)
            {
                existing.UnitPrice = unitPrice;
                existing.UpdatedAt = DateTime.Now;
            }
            else
            {
                db.ItemPrices.Add(new ItemPrice
                {
                    MonsterId = monsterId,
                    ItemName = itemName,
                    UnitPrice = unitPrice,
                    UpdatedAt = DateTime.Now
                });
            }
            await db.SaveChangesAsync();
        }

        // DatabaseService.cs에 추가

        // 몬스터의 아이템 목록 조회
        public async Task<List<MonsterItem>> GetMonsterItemsAsync(int monsterId)
        {
            using var db = new AppDbContext();
            return await db.MonsterItems
                .Where(x => x.MonsterId == monsterId)
                .OrderBy(x => x.ItemName)
                .ToListAsync();
        }

        // 아이템 추가
        public async Task AddMonsterItemAsync(int monsterId, string itemName)
        {
            using var db = new AppDbContext();
            // 중복 방지
            bool exists = await db.MonsterItems
                .AnyAsync(x => x.MonsterId == monsterId && x.ItemName == itemName);
            if (exists) return;

            db.MonsterItems.Add(new MonsterItem
            {
                MonsterId = monsterId,
                ItemName = itemName.Trim()
            });
            await db.SaveChangesAsync();
        }

        // 아이템 삭제
        public async Task DeleteMonsterItemAsync(int id)
        {
            using var db = new AppDbContext();
            var item = await db.MonsterItems.FindAsync(id);
            if (item != null)
            {
                db.MonsterItems.Remove(item);
                await db.SaveChangesAsync();
            }
        }

        public async Task UpdateDropLogUnitPriceAsync(int dropLogId, long unitPrice)
        {
            using var db = new AppDbContext();
            var log = await db.DropLogs.FindAsync(dropLogId);
            if (log != null)
            {
                log.UnitPrice = unitPrice;
                await db.SaveChangesAsync();
            }
        }

        // 세션의 드롭 로그 전체 동기화 (수량 수정 및 신규 추가 저장)
        public async Task SyncDropLogsAsync(int sessionId, List<PriceItemSummary> items)
        {
            using var db = new AppDbContext();
            var existingLogs = await db.DropLogs
                .Where(d => d.SessionId == sessionId)
                .ToListAsync();

            // 1. 현재 화면에 있는 아이템들 처리
            foreach (var item in items)
            {
                var logsOfItem = existingLogs.Where(l => l.ItemName == item.ItemName).ToList();

                if (logsOfItem.Any())
                {
                    // 기존 로그가 있는 경우: 모두 삭제 후 사용자가 수정한 수량으로 통합된 하나의 로그 생성
                    // (개별 드롭 시간보다는 전체 통계의 정확성이 우선되는 화면이므로 병합 처리)
                    var firstDroppedAt = logsOfItem.OrderBy(l => l.DroppedAt).First().DroppedAt;
                    db.DropLogs.RemoveRange(logsOfItem);

                    if (item.TotalQuantity > 0)
                    {
                        db.DropLogs.Add(new DropLog
                        {
                            SessionId = sessionId,
                            ItemName = item.ItemName,
                            Quantity = item.TotalQuantity,
                            UnitPrice = item.UnitPrice,
                            DroppedAt = firstDroppedAt
                        });
                    }
                }
                else
                {
                    // 기존 로그가 없는 경우 (수동 추가된 아이템): 신규 생성
                    if (item.TotalQuantity > 0)
                    {
                        db.DropLogs.Add(new DropLog
                        {
                            SessionId = sessionId,
                            ItemName = item.ItemName,
                            Quantity = item.TotalQuantity,
                            UnitPrice = item.UnitPrice,
                            DroppedAt = DateTime.Now
                        });
                    }
                }
            }

            // 2. 사용자가 목록에서 삭제한 아이템 처리
            var itemNamesInView = items.Select(i => i.ItemName).ToHashSet();
            var logsToDelete = existingLogs.Where(l => !itemNamesInView.Contains(l.ItemName));
            db.DropLogs.RemoveRange(logsToDelete);

            await db.SaveChangesAsync();
        }
    }


    // 통계용 모델 (Service 내부)
    public class PriceItemSummary
    {
        public string ItemName { get; set; } = string.Empty;
        public int TotalQuantity { get; set; }
        public long UnitPrice { get; set; }
    }
}