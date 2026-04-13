namespace StudioStudio_Server.Models.DTOs.Response
{
    public class GroupDetailResponse
    {
        public Guid GroupId { get; set; }
        public string GroupName { get; set; } = string.Empty;
        public string? Description { get; set; }
        public Guid? StudioId { get; set; }
        public string? StudioName { get; set; }
        public string GroupType { get; set; } = string.Empty;
        public bool IsFavorite { get; set; }
        public bool IsTemplate { get; set; }
        public Guid? TemplateId { get; set; }
        public UserDto CreatedBy { get; set; } = null!;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public int MemberCount { get; set; }
        public int TaskCount { get; set; }
        public string UserRole { get; set; } = string.Empty;
        public List<TaskStatusDto> TaskStatuses { get; set; } = new();

        // 🔹 ADDED: Group personalization
        public string? AvatarUrl { get; set; }
        public string? ColorHex { get; set; }
        public string? IconEmoji { get; set; }
        public string? BannerUrl { get; set; }
        public string? Tagline { get; set; }
        public string? Alias { get; set; }
        public bool IsOpen { get; set; }
        public bool IsArchived { get; set; }
        public bool AllowMemberUpdateProgress { get; set; }
    }

    public class TaskStatusDto
    {
        public Guid StatusId { get; set; }
        public string StatusName { get; set; } = string.Empty;
        public int Position { get; set; }
        public List<TaskItemResponse>? TaskList { get; set; }
    }
}
