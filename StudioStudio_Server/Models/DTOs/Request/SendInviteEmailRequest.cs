using System.ComponentModel.DataAnnotations;
using StudioStudio_Server.Exceptions;

namespace StudioStudio_Server.Models.DTOs.Request
{
    public class SendInviteEmailRequest
    {
        [Required(ErrorMessage = ErrorCodes.ValidationRequiredField)]
        public Guid GroupId { get; set; }

        [Required(ErrorMessage = ErrorCodes.ValidationRequiredField)]
        public string Role { get; set; } = string.Empty;

        [Required(ErrorMessage = ErrorCodes.ValidationRequiredField)]
        [EmailAddress(ErrorMessage = ErrorCodes.ValidationInvalidEmail)]
        public string Email { get; set; } = string.Empty;
    }
}
