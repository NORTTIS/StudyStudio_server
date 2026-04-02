using System.ComponentModel.DataAnnotations;

namespace StudioStudio_Server.Models.DTOs.Request
{
    public class ToggleIsOpenRequest
    {
        [Required]
        public bool IsOpen { get; set; }
    }
}
