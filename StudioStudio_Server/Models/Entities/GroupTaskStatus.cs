namespace StudioStudio_Server.Models.Entities
{
    public class GroupTaskStatus
    {
        public Guid StatusId { get; set; }
        public Guid GroupId { get; set; }
        public bool IsDeleted { get; set; } = false;
        public string StatusName { get; set; } = null!;
        public int Position { get; set; }
    }
}