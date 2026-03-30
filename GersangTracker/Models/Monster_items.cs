namespace GersangTracker.Models
{
    public class MonsterItem
    {
        public int Id { get; set; }
        public int MonsterId { get; set; }
        public string ItemName { get; set; } = string.Empty;
        public Monster? Monster { get; set; }
    }
}
