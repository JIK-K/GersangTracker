namespace GersangTracker.Models
{
    public class ClientInstance
    {
        public int Id { get; set; }
        public int ClientIndex { get; set; }
        public int? AccountId { get; set; }
        public Account? Account { get; set;  }

        public string InstallPath { get; set; } = string.Empty;
    }
}
