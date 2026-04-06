using System.ComponentModel.DataAnnotations;
using StudioStudio_Server.Exceptions;

namespace StudioStudio_Server.Models.DTOs.Request
{
    public class UpdateTemplateRequest
    {
        [Required(ErrorMessage = ErrorCodes.ValidationRequiredField)]
        public Guid GroupId { get; set; }

        public bool IsSystemTemplate { get; set; }

        /// <summary>
        /// Toggle template visibility (active/inactive).
        /// Only applies to system templates.
        /// </summary>
        public bool? IsActive { get; set; }

        /// <summary>
        /// Optional: rename the template's group name.
        /// </summary>
        public string? GroupName { get; set; }

        /// <summary>
        /// Optional: update the template's description.
        /// </summary>
        public string? GroupDescription { get; set; }

        /// <summary>
        /// Optional: update the full list of group task statuses.
        /// If not null → hard-delete all existing statuses and replace with the new list.
        /// </summary>
        public List<TemplateTaskStatusRequest>? GroupTaskStatuses { get; set; }
    }
}
