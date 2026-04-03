using System.ComponentModel.DataAnnotations;

namespace StudioStudio_Server.Models.DTOs.Request
{
    public class ToggleArchiveRequest
    {
        [Required]
        public bool IsArchived { get; set; }
    }
}
