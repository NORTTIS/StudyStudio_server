using System.ComponentModel.DataAnnotations;
using StudioStudio_Server.Exceptions;

namespace StudioStudio_Server.Models.DTOs.Request
{
    /// <summary>
    /// Request ð? h?i AI v? group documents và tasks
    /// </summary>
    public class AIQuestionRequest
    {
        [Required(ErrorMessage = ErrorCodes.ValidationRequiredField)]
        public Guid GroupId { get; set; }

        [Required(ErrorMessage = ErrorCodes.ValidationRequiredField)]
        [StringLength(1000, MinimumLength = 3, ErrorMessage = ErrorCodes.ValidationRequiredField)]
        public string Question { get; set; } = string.Empty;
    }
}
