using System.ComponentModel.DataAnnotations;
using StudioStudio_Server.Exceptions;

namespace StudioStudio_Server.Models.DTOs.Request
{
    public class CreateInviteLinkRequest
    {
        [Required(ErrorMessage = ErrorCodes.ValidationRequiredField)]
        public Guid GroupId { get; set; }

        [Required(ErrorMessage = ErrorCodes.ValidationRequiredField)]
        public string Role { get; set; } = string.Empty;
    }
}
