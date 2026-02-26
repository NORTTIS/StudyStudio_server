namespace StudioStudio_Server.Models.Entities
{
    public class EmailVerificationDataRedis
    {
        public string Email { get; set; } = string.Empty;
        public Guid UserId { get; set; }
        public string Token { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }
}
