namespace GersangTracker.Models
{
    public class Monster
    {
        public int Id { get; set; }
        public int? AccountId { get; set; }
        public Account? Account { get; set; }
        public string Name { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public List<Session> Sessions { get; set; } = new();
        public List<ItemPrice> ItemPrices { get; set; } = new();
    }
}