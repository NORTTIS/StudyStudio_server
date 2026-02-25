namespace StudioStudio_Server.Models.Entities
{
    public class GroupMessage
    {
        public Guid MessageId { get; set; }
        public Guid GroupId { get; set; }
        public Guid UserId { get; set; }
        public string Content { get; set; } = null!;
        
        public Guid? ParentMessageId { get; set; }
        
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public bool IsDeleted { get; set; }

        public Group Group { get; set; } = null!;
        public User User { get; set; } = null!;
        
        public GroupMessage? ParentMessage { get; set; }
        public List<GroupMessage> Replies { get; set; } = new();
    }
}
