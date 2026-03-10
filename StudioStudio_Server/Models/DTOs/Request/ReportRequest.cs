using System.ComponentModel.DataAnnotations;
using StudioStudio_Server.Exceptions;
using StudioStudio_Server.Models.Enums;

namespace StudioStudio_Server.Models.DTOs.Request
{
    public class ReportRequest
    {
        [Required(ErrorMessage = ErrorCodes.ValidationRequiredField)]
        public ReportType Type { get; set; }

        [Required(ErrorMessage = ErrorCodes.ValidationRequiredField)]
        [EmailAddress(ErrorMessage = ErrorCodes.ValidationInvalidEmail)]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = ErrorCodes.ValidationRequiredField)]
        [StringLength(100, MinimumLength = 1, ErrorMessage = ErrorCodes.ValidationRequiredField)]
        public string Title { get; set; } = string.Empty;

        [Required(ErrorMessage = ErrorCodes.ValidationRequiredField)]
        [StringLength(500, MinimumLength = 1, ErrorMessage = ErrorCodes.ValidationRequiredField)]
        public string Content { get; set; } = string.Empty;
    }
}
