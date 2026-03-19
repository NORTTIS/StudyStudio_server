namespace StudioStudio_Server.Models.DTOs.Request
{
    public class CreateStudioRequest
    {
        public string StudioName { get; set; } = null!;
        public string? Description { get; set; }

        public Guid OwnerId { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
    }
}
