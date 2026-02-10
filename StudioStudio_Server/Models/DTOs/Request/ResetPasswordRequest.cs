using System.ComponentModel.DataAnnotations;
using StudioStudio_Server.Exceptions;

namespace StudioStudio_Server.Models.DTOs.Request
{
    public class ResetPasswordRequest
    {
        [Required(ErrorMessage = ErrorCodes.ValidationRequiredField)]
        public string Token { get; set; } = string.Empty;

        [Required(ErrorMessage = ErrorCodes.ValidationRequiredField)]
        [StringLength(20, MinimumLength = 10, ErrorMessage = ErrorCodes.ValidationInvalidPassword)]
        public string NewPassword { get; set; } = string.Empty;
    }
}
