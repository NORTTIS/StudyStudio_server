using System.ComponentModel.DataAnnotations;

namespace StudioStudio_Server.Models.DTOs.Request
{
    public class RemoveFavouriteRequest
    {
        [Required]
        public Guid GroupId { get; set; }
    }
}
