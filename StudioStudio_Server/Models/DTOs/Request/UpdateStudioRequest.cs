namespace StudioStudio_Server.Models.DTOs.Request
{
    public class UpdateStudioRequest
    {
        public Guid Id { get; set; }
        public string StudioName { get; set; } = null!;
        public string? Description { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
    }
}
