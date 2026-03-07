namespace StudioStudio_Server.Models.DTOs.Response
{
    public class PersonalTaskStatusResponse
    {
        public Guid StatusId { get; set; }
        public string StatusName { get; set; } = null!;
        public int Position { get; set; }
    }
}
