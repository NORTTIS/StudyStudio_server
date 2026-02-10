using System.ComponentModel.DataAnnotations;
using StudioStudio_Server.Exceptions;

namespace StudioStudio_Server.Models.DTOs.Request
{
    public class ReportRequest
    {
        [Required(ErrorMessage = ErrorCodes.ValidationRequiredField)]
        public string Type { get; set; } = string.Empty;

        [Required(ErrorMessage = ErrorCodes.ValidationRequiredField)]
        [EmailAddress(ErrorMessage = ErrorCodes.ValidationInvalidEmail)]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = ErrorCodes.ValidationRequiredField)]
        [StringLength(200, MinimumLength = 1, ErrorMessage = ErrorCodes.ValidationRequiredField)]
        public string Title { get; set; } = string.Empty;

        [Required(ErrorMessage = ErrorCodes.ValidationRequiredField)]
        [StringLength(2000, MinimumLength = 1, ErrorMessage = ErrorCodes.ValidationRequiredField)]
        public string Content { get; set; } = string.Empty;
    }
}
