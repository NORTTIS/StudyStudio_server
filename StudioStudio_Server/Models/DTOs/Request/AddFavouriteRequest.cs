using System.ComponentModel.DataAnnotations;

namespace StudioStudio_Server.Models.DTOs.Request
{
    public class AddFavouriteRequest
    {
        [Required]
        public Guid GroupId { get; set; }
    }
}
