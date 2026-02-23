namespace StudioStudio_Server.Models.DTOs.Response
{
    public class StudioResponse
    {
        public Guid StudioId { get; set; }
        public string StudioName { get; set; } = string.Empty;
        public string? Description { get; set; }
        public Guid OwnerId { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public int GroupCount { get; set; }
    }
}
