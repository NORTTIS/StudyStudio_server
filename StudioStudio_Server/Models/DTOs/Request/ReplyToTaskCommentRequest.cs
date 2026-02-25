using System.ComponentModel.DataAnnotations;
using StudioStudio_Server.Exceptions;

namespace StudioStudio_Server.Models.DTOs.Request
{
    public class ReplyToTaskCommentRequest
    {
        public Guid TaskId { get; set; }
        public Guid ParentCommentId { get; set; }
        
        [Required(ErrorMessage = ErrorCodes.ValidationRequiredField)]
        [StringLength(5000, ErrorMessage = ErrorCodes.ValidationRequiredField)]
        public string Content { get; set; } = string.Empty;
    }
}
