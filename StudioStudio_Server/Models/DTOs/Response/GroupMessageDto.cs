namespace StudioStudio_Server.Models.DTOs.Response
{
    public class GroupMessageDto
    {
        public Guid MessageId { get; set; }
        public Guid GroupId { get; set; }
        public Guid UserId { get; set; }
        public string Content { get; set; } = string.Empty;
        public Guid? ParentMessageId { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public bool IsDeleted { get; set; }
        public UserDto User { get; set; } = null!;
        public int ReplyCount { get; set; }
        public List<GroupMessageDto>? Replies { get; set; }
    }

    public class GroupMessageListResponse
    {
        public Guid GroupId { get; set; }
        public int TotalMessages { get; set; }
        public List<GroupMessageDto> Messages { get; set; } = new();
    }
}
