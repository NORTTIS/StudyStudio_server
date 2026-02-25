namespace StudioStudio_Server.Models.DTOs.Response
{
    public class TaskCommentDto
    {
        public Guid CommentId { get; set; }
        public Guid TaskId { get; set; }
        public Guid UserId { get; set; }
        public string Content { get; set; } = string.Empty;
        public Guid? ParentCommentId { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public bool IsDeleted { get; set; }
        public UserDto User { get; set; } = null!;
        public int ReplyCount { get; set; }
        public List<TaskCommentDto>? Replies { get; set; }
    }

    public class TaskCommentListResponse
    {
        public Guid TaskId { get; set; }
        public int TotalComments { get; set; }
        public List<TaskCommentDto> Comments { get; set; } = new();
    }
}
