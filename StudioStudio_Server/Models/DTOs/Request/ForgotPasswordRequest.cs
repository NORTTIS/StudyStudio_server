using System.ComponentModel.DataAnnotations;
using StudioStudio_Server.Exceptions;

namespace StudioStudio_Server.Models.DTOs.Request
{
    public class ForgotPasswordRequest
    {
        [Required(ErrorMessage = ErrorCodes.ValidationRequiredField)]
        [EmailAddress(ErrorMessage = ErrorCodes.ValidationInvalidEmail)]
        public string Email { get; set; } = string.Empty;
    }
}
