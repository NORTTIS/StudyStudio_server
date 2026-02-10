using System.ComponentModel.DataAnnotations;
using StudioStudio_Server.Exceptions;

namespace StudioStudio_Server.Models.DTOs.Request
{
    public class UpdateUserProfileRequest
    {
        [StringLength(50, MinimumLength = 1, ErrorMessage = ErrorCodes.ValidationRequiredField)]
        public string? FirstName { get; set; }

        [StringLength(50, MinimumLength = 1, ErrorMessage = ErrorCodes.ValidationRequiredField)]
        public string? LastName { get; set; }

        [Phone(ErrorMessage = ErrorCodes.ValidationInvalidEmail)]
        public string? PhoneNumber { get; set; }

        [StringLength(500, ErrorMessage = ErrorCodes.ValidationRequiredField)]
        public string? Bio { get; set; }

        [StringLength(10, MinimumLength = 2, ErrorMessage = ErrorCodes.ValidationRequiredField)]
        public string? Language { get; set; }

        public bool? EmailNotificationEnabled { get; set; }

        public IFormFile? Avatar { get; set; }
    }
}
