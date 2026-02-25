using StudioStudio_Server.Exceptions;
using System.ComponentModel.DataAnnotations;

namespace StudioStudio_Server.Models.DTOs.Request
{
    public class CreateStudioGroupsRequest
    {
        public Guid? StudioId { get; set; } = null;

        [Required(ErrorMessage = ErrorCodes.ValidationRequiredField)]
        [StringLength(100, MinimumLength = 1, ErrorMessage = ErrorCodes.ValidationRequiredField)]
        public string GroupPrefix { get; set; } = string.Empty;

        [Required(ErrorMessage = ErrorCodes.ValidationGroupCreationNumber)]
        [Range(1, int.MinValue, ErrorMessage = ErrorCodes.ValidationGroupCreationNumber)]
        public int GroupCount { get; set; }

        [StringLength(500, ErrorMessage = ErrorCodes.ValidationRequiredField)]
        public string? Description { get; set; }

        public Guid? TemplateId { get; set; } = null;
    }
}
