using System.ComponentModel.DataAnnotations;
using StudioStudio_Server.Exceptions;
using StudioStudio_Server.Models.Enums;

namespace StudioStudio_Server.Models.DTOs.Request
{
    public class UpdateAnnouncementRequest
    {
        [Required(ErrorMessage = ErrorCodes.ValidationRequiredField)]
        public Guid AnnouncementId { get; set; }

        [Required(ErrorMessage = ErrorCodes.ValidationRequiredField)]
        [StringLength(200, MinimumLength = 1, ErrorMessage = ErrorCodes.ValidationRequiredField)]
        public string Title { get; set; } = string.Empty;

        [Required(ErrorMessage = ErrorCodes.ValidationRequiredField)]
        [StringLength(2000, MinimumLength = 1, ErrorMessage = ErrorCodes.ValidationRequiredField)]
        public string Content { get; set; } = string.Empty;

        [Required(ErrorMessage = ErrorCodes.ValidationRequiredField)]
        public AnnouncementType Type { get; set; }

        public bool IsActive { get; set; }
        public DateTime? PublishedAt { get; set; }
    }
}
