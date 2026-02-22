using System.ComponentModel.DataAnnotations;
using StudioStudio_Server.Exceptions;

namespace StudioStudio_Server.Models.DTOs.Request
{
    public class GroupTaskStatusRequest
    {
        [Required(ErrorMessage = ErrorCodes.ValidationRequiredField)]
        [StringLength(50, MinimumLength = 1, ErrorMessage = ErrorCodes.ValidationRequiredField)]
        public string StatusName { get; set; } = string.Empty;

        [Required(ErrorMessage = ErrorCodes.ValidationRequiredField)]
        public int Position { get; set; }
    }
}
