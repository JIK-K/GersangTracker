namespace GersangTracker.Models
{
    public class Account
    {
        public int Id { get; set; }
        public string UserId { get; set; } = string.Empty;
        public string EncryptedPassword { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public ClientInstance? ClientInstance { get; set; }

    }
}
