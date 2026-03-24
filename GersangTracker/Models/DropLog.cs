namespace GersangTracker.Models
{
    public class DropLog
    {
        public int Id { get; set; }
        public int SessionId { get; set; }
        public DateTime DroppedAt { get; set; }
        public string ItemName { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public long UnitPrice { get; set; }

        public Session Session { get; set; } = null!;
    }
}