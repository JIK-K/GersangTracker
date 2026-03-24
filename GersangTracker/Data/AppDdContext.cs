using GersangTracker.Models;
using Microsoft.EntityFrameworkCore;
using System.IO;

namespace GersangTracker.Data
{
    public class AppDbContext : DbContext
    {
        public DbSet<Monster> Monsters { get; set; }
        public DbSet<Session> Sessions { get; set; }
        public DbSet<DropLog> DropLogs { get; set; }
        public DbSet<ItemPrice> ItemPrices { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            //경로-AppData\Roaming\GersangTracker
            string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string dbFolder = Path.Combine(appDataPath, "GersangTracker");

            Directory.CreateDirectory(dbFolder);

            string dbPath = Path.Combine(dbFolder, "GersangTracker.db");
            optionsBuilder.UseSqlite($"Data Source={dbPath}");
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Monster>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Name).IsRequired();
                entity.Property(e => e.CreatedAt).IsRequired();
            });
            // (몬스터 1 : 세션 N)
            modelBuilder.Entity<Session>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasOne(e => e.Monster)
                      .WithMany(m => m.Sessions)
                      .HasForeignKey(e => e.MonsterId);
            });

            //  (세션 1 : 드랍로그 N)
            modelBuilder.Entity<DropLog>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasOne(e => e.Session)
                      .WithMany(s => s.DropLogs)
                      .HasForeignKey(e => e.SessionId);
            });

            //  (몬스터 1 : 아이템단가 N)
            modelBuilder.Entity<ItemPrice>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasOne(e => e.Monster)
                      .WithMany(m => m.ItemPrices)
                      .HasForeignKey(e => e.MonsterId);
            });

        }
    }
}