namespace StudioStudio_Server.Models.DTOs.Response
{
    public class GroupTaskStatusResponse
    {
        public Guid StatusId { get; set; }
        public Guid GroupId { get; set; }
        public string StatusName { get; set; } = null!;
        public int Position { get; set; }
    }
}
