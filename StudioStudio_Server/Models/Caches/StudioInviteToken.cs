namespace StudioStudio_Server.Models.Caches
{
    public class StudioInviteToken
    {
        public Guid StudioId { get; set; }
        public string Role { get; set; } = string.Empty;
        public Guid CreatedBy { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
