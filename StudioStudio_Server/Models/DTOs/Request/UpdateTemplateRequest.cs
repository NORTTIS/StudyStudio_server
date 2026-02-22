using System.ComponentModel.DataAnnotations;
using StudioStudio_Server.Exceptions;

namespace StudioStudio_Server.Models.DTOs.Request
{
    public class UpdateTemplateRequest
    {
        [Required(ErrorMessage = ErrorCodes.ValidationRequiredField)]
        public Guid GroupId { get; set; }

        public bool IsSystemTemplate { get; set; }
    }
}
