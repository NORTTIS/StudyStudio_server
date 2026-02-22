using System.ComponentModel.DataAnnotations;
using StudioStudio_Server.Exceptions;

namespace StudioStudio_Server.Models.DTOs.Request
{
    public class CreateGroupRequest
    {
        public Guid? StudioId { get; set; } = null;

        [Required(ErrorMessage = ErrorCodes.ValidationRequiredField)]
        [StringLength(100, MinimumLength = 1, ErrorMessage = ErrorCodes.ValidationRequiredField)]
        public string GroupName { get; set; } = string.Empty;

        [StringLength(500, ErrorMessage = ErrorCodes.ValidationRequiredField)]
        public string? Description { get; set; }
    }
}
