using StudioStudio_Server.Exceptions;
using System.ComponentModel.DataAnnotations;

namespace StudioStudio_Server.Models.DTOs.Request
{
    public class PersonalTaskStatusRequest
    {
        [Required(ErrorMessage = ErrorCodes.ValidationRequiredField)]
        [StringLength(50, MinimumLength = 1, ErrorMessage = ErrorCodes.ValidationRequiredField)]
        public string StatusName { get; set; } = string.Empty;
    }

    public class ReorderPersonalTaskStatusRequest
    {
        [Required(ErrorMessage = ErrorCodes.ValidationRequiredField)]
        public Guid StatusId { get; set; }

        public Guid? PrevStatusId { get; set; }

        public Guid? NextStatusId { get; set; }
    }
}
