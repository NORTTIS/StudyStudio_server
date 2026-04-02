namespace StudioStudio_Server.Models.DTOs.Response
{
    public class ToggleIsOpenResponse
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public bool IsOpen { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
