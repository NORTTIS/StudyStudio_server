using System.ComponentModel.DataAnnotations;

namespace StudioStudio_Server.Models.DTOs.Request
{
    public class CompleteAvatarUploadRequest
    {
        [Required]
        public string FileKey { get; set; } = null!;
    }
}
