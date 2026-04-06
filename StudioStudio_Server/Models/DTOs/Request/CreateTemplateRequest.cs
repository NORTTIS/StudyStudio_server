using System.ComponentModel.DataAnnotations;
using StudioStudio_Server.Exceptions;

namespace StudioStudio_Server.Models.DTOs.Request
{
    public class CreateTemplateRequest
    {
        [Required(ErrorMessage = ErrorCodes.ValidationRequiredField)]
        [StringLength(100, MinimumLength = 1, ErrorMessage = ErrorCodes.ValidationRequiredField)]
        public string GroupName { get; set; } = string.Empty;

        [StringLength(500, ErrorMessage = ErrorCodes.ValidationRequiredField)]
        public string? Description { get; set; }

        [Required(ErrorMessage = ErrorCodes.ValidationRequiredField)]
        [MinLength(1, ErrorMessage = ErrorCodes.ValidationRequiredField)]
        public List<TemplateTaskStatusRequest> GroupTaskStatuses { get; set; } = new List<TemplateTaskStatusRequest>();

        /// <summary>
        /// Optional: set initial IsActive state.
        /// Defaults to false (admin-created templates start inactive).
        /// </summary>
        public bool IsActive { get; set; } = false;
    }
}
