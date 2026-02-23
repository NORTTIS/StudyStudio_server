namespace StudioStudio_Server.Models.DTOs.Response
{
    public class FavouriteResponse
    {
        public Guid FavouriteId { get; set; }
        public Guid GroupId { get; set; }
        public string GroupName { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }
}
