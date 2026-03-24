namespace GersangTracker.Models
{
    public class ItemPrice
    {
        public int Id { get; set; }
        public int MonsterId { get; set; }
        public string ItemName { get; set; } = string.Empty;
        public long UnitPrice { get; set; }
        public DateTime UpdatedAt { get; set; } = DateTime.Now;

        public Monster Monster { get; set; } = null!;
    }
}