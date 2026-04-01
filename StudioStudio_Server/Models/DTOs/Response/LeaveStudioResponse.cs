namespace StudioStudio_Server.Models.DTOs.Response
{
    public class LeaveStudioResponse
    {
        public Guid StudioId { get; set; }
        public string StudioName { get; set; } = string.Empty;
        public DateTime LeftAt { get; set; }
    }
}
