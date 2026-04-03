namespace StudioStudio_Server.Models.DTOs.Response
{
    public class ArchiveGroupResponse
    {
        public Guid GroupId { get; set; }
        public bool IsArchived { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public class ArchiveStudioResponse
    {
        public Guid StudioId { get; set; }
        public bool IsArchived { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
