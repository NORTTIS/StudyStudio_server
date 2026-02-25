namespace StudioStudio_Server.Models.Entities
{
    public class TaskComment
    {
        public Guid CommentId { get; set; }
        public Guid TaskId { get; set; }
        public Guid UserId { get; set; }
        public string Content { get; set; } = null!;
        
        public Guid? ParentCommentId { get; set; }
        
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public bool IsDeleted { get; set; }

        public TaskItem Task { get; set; } = null!;
        public User User { get; set; } = null!;
        
        public TaskComment? ParentComment { get; set; }
        public List<TaskComment> Replies { get; set; } = new();
    }
}
