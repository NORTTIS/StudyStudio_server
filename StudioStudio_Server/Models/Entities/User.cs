namespace StudioStudio_Server.Models.Entities
{
    public class User
    {
        public Guid UserId { get; set; }
        public string Email { get; set; }
        public string? PasswordHash { get; set; }
        public string FullName { get; set; }
        public string? AvatarUrl { get; set; }
        public UserStatus Status { get; set; }
        public bool IsAdmin { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public string Language { get; set; }
        public bool EmailNotificationEnabled { get; set; }
        public ICollection<RefreshToken> RefreshTokens { get; set; }
        public ICollection<EmailVerificationToken> EmailVerificationTokens { get; set; }
        public string? GoogleId { get; set; }
        public ICollection<GroupParticipant> GroupParticipants { get; set; }
    }

}