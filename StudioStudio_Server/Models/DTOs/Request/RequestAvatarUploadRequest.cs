using System.ComponentModel.DataAnnotations;

namespace StudioStudio_Server.Models.DTOs.Request
{
    public class RequestAvatarUploadRequest
    {
        [Required]
        public string ContentType { get; set; } = null!;

        [Required]
        public long FileSize { get; set; }
    }
}
