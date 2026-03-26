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

        // [DropLog] - [Create] 드랍 로그 추가
        public async Task AddDropLogAsync(int sessionId, string itemName, int quantity)
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

        // [DropLog] - [Read] 세션별 드랍 로그 조회
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
    }
}