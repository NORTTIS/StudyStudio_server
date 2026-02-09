namespace StudioStudio_Server.Models.Entities
{
    public class User
    {
        public Guid UserId { get; set; }

        public string Email { get; set; } = null!;
        public string PasswordHash { get; set; } = null!;

        public string FullName { get; set; } = null!;
        public string? AvatarUrl { get; set; }

        public UserStatus Status { get; set; }
        public bool IsAdmin { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        public string Language { get; set; } = "vi";
        public bool EmailNotificationEnabled { get; set; } = true;
        public RefreshToken? RefreshToken { get; set; }
        public ICollection<EmailVerificationToken> EmailVerificationToken { get; set; } = new List<EmailVerificationToken>();
        public string GoogleId { get; set; } = null;
        public ICollection<GroupParticipant> GroupParticipants { get; set; } = new List<GroupParticipant>();
    }
}