using System.ComponentModel.DataAnnotations;
using StudioStudio_Server.Exceptions;

namespace StudioStudio_Server.Models.DTOs.Request
{
    public class RefreshTokenRequest
    {
        [Required(ErrorMessage = ErrorCodes.ValidationRequiredField)]
        public string RefreshToken { get; set; } = string.Empty;
    }
}
