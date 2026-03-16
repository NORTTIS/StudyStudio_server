using System.ComponentModel.DataAnnotations;
using StudioStudio_Server.Exceptions;

namespace StudioStudio_Server.Models.DTOs.Request
{
    public class CreateStudioInviteRequest
    {
        [Required(ErrorMessage = ErrorCodes.ValidationRequiredField)]
        public Guid StudioId { get; set; }

        [Required(ErrorMessage = ErrorCodes.ValidationRequiredField)]
        public string Role { get; set; } = string.Empty;
    }
}
