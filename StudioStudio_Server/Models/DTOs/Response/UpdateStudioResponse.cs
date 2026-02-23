namespace StudioStudio_Server.Models.DTOs.Response
{
    public class UpdateStudioResponse
    {
        public string StudioName { get; set; } = null!;
        public string? Description { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
