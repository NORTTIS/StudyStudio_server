using System.ComponentModel.DataAnnotations;
using StudioStudio_Server.Exceptions;

namespace StudioStudio_Server.Models.DTOs.Request
{
    public class AcceptStudioInviteRequest
    {
        [Required(ErrorMessage = ErrorCodes.ValidationRequiredField)]
        public string Token { get; set; } = string.Empty;
    }
}
