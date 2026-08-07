using GersangTracker.Data;
using GersangTracker.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace GersangTracker.Services
{
    public class DatabaseService
    {
        // [Account] - 계정 전체 로드 (ClientInstance도 함께 불러옴)
        public async Task<List<Account>> GetAccountsAsync()
        {
            using var db = new AppDbContext();
            return await db.Account
                           .Include(a => a.ClientInstance)
                           .ToListAsync();
        }

        // [Account] - 계정 생성 및 수정
        public async Task AddOrUpdateAccountAsync(Account account)
        {
            using var db = new AppDbContext();
            if (account.Id == 0)
            {
                // 새 계정
                db.Account.Add(account);
            }
            else
            {
                // 기존 계정
                db.Account.Update(account);
            }
            await db.SaveChangesAsync();
        }

        // [Account] - 계정 삭제 (연관된 설정, 몬스터 기록 모두 함께 삭제됨)
        public async Task DeleteAccountAsync(int accountId)
        {
            using var db = new AppDbContext();
            var account = await db.Account.FindAsync(accountId);
            if (account != null)
            {
                db.Account.Remove(account);
                await db.SaveChangesAsync();
            }
        }

        // [Monster] - [Read] 계정별 몬스터 전체 조회
        public async Task<List<Monster>> GetMonstersByAccountIdAsync(int accountId)
        {
            using var db = new AppDbContext();
            return await db.Monsters
                           .Where(m => m.AccountId == accountId)
                           .ToListAsync();
        }

        // [Monster] - [Create] 몬스터 추가
        public async Task AddMonsterAsync(int accountId, string name)
        {
            using var db = new AppDbContext();
            var isExist = await db.Monsters.AnyAsync(m => m.AccountId == accountId && m.Name == name);
            if (isExist)
            {
                throw new InvalidOperationException("이미 존재하는 몬스터 이름 입니다.");
            }
            db.Monsters.Add(new Monster { AccountId = accountId, Name = name });
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

        // [Session] - [Update] 세션 수익만 업데이트
        public async Task UpdateSessionProfitAsync(int sessionId, long totalProfit)
        {
            using var db = new AppDbContext();
            var session = await db.Sessions.FindAsync(sessionId);
            if (session == null) return;
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

        // [DropLog] - [Create] 드랍 로그 추가
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

        // [ItemPrice] - [Create/Update] 아이템 단가 저장
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

        // 세션의 드롭 로그 전체 동기화
        public async Task SyncDropLogsAsync(int sessionId, List<PriceItemSummary> items)
        {
            using var db = new AppDbContext();
            var existingLogs = await db.DropLogs
                .Where(d => d.SessionId == sessionId)
                .ToListAsync();

            foreach (var item in items)
            {
                var logsOfItem = existingLogs.Where(l => l.ItemName == item.ItemName).ToList();

                if (logsOfItem.Any())
                {
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

            var itemNamesInView = items.Select(i => i.ItemName).ToHashSet();
            var logsToDelete = existingLogs.Where(l => !itemNamesInView.Contains(l.ItemName));
            db.DropLogs.RemoveRange(logsToDelete);

            await db.SaveChangesAsync();
        }
    }

    public class PriceItemSummary
    {
        public string ItemName { get; set; } = string.Empty;
        public int TotalQuantity { get; set; }
        public long UnitPrice { get; set; }
    }
}