namespace StudioStudio_Server.Models.Entities
{
    public class Favourite
    {
        public Guid FavouriteId { get; set; }

        public Guid UserId { get; set; }
        public Guid GroupId { get; set; }

        public DateTime CreatedAt { get; set; }
    }
}