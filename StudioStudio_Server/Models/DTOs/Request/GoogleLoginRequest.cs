using System.ComponentModel.DataAnnotations;
using StudioStudio_Server.Exceptions;

namespace StudioStudio_Server.Models.DTOs.Request
{
    public class GoogleLoginRequest
    {
        [Required(ErrorMessage = ErrorCodes.ValidationRequiredField)]
        public string IdToken { get; set; } = string.Empty;
    }
}
