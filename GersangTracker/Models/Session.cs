namespace GersangTracker.Models
{
    public class Session
    {
        public int Id { get; set; }
        public int MonsterId { get; set; }
        public DateTime StartedAt { get; set; }
        public DateTime EndedAt { get; set; }
        public long TotalProfit { get; set; }

        public Monster Monster { get; set; } = null!;
        public List<DropLog> DropLogs { get; set; } = new();
    }
}