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
        // 사냥 시간 표시용 프로퍼티
        public string HuntingTimeDisplay
        {
            get
            {
                var elapsed = EndedAt - StartedAt;
                if (elapsed.TotalSeconds < 60)
                    return $"{(int)elapsed.TotalSeconds}초";
                else if (elapsed.TotalHours < 1)
                    return $"{elapsed.Minutes}분 {elapsed.Seconds}초";
                else
                    return $"{(int)elapsed.TotalHours}시간 {elapsed.Minutes}분";
            }
        }
    }
}