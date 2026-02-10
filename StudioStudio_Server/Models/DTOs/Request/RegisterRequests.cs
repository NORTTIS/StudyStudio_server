using System.ComponentModel.DataAnnotations;
using StudioStudio_Server.Exceptions;

namespace StudioStudio_Server.Models.DTOs.Request
{
    public class RegisterRequests
    {
        [Required(ErrorMessage = ErrorCodes.ValidationRequiredField)]
        [EmailAddress(ErrorMessage = ErrorCodes.ValidationInvalidEmail)]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = ErrorCodes.ValidationRequiredField)]
        [StringLength(20, MinimumLength = 10, ErrorMessage = ErrorCodes.ValidationInvalidPassword)]
        public string Password { get; set; } = string.Empty;

        [Required(ErrorMessage = ErrorCodes.ValidationRequiredField)]
        [Compare("Password", ErrorMessage = ErrorCodes.ValidationPasswordMismatch)]
        public string ConfirmPassword { get; set; } = string.Empty;

        [Required(ErrorMessage = ErrorCodes.ValidationRequiredField)]
        [StringLength(50, MinimumLength = 1, ErrorMessage = ErrorCodes.ValidationRequiredField)]
        public string FirstName { get; set; } = string.Empty;

        [Required(ErrorMessage = ErrorCodes.ValidationRequiredField)]
        [StringLength(50, MinimumLength = 1, ErrorMessage = ErrorCodes.ValidationRequiredField)]
        public string LastName { get; set; } = string.Empty;
    }
}
