using System.ComponentModel.DataAnnotations;
using StudioStudio_Server.Exceptions;

namespace StudioStudio_Server.Models.DTOs.Request
{
    /// <summary>
    /// Request ð? t?o presigned URL cho document upload
    /// </summary>
    public class RequestDocumentUploadRequest
    {
        [Required(ErrorMessage = ErrorCodes.ValidationRequiredField)]
        public Guid GroupId { get; set; }

        [Required(ErrorMessage = ErrorCodes.ValidationRequiredField)]
        [StringLength(255, MinimumLength = 1, ErrorMessage = ErrorCodes.ValidationRequiredField)]
        public string FileName { get; set; } = string.Empty;

        [Required(ErrorMessage = ErrorCodes.ValidationRequiredField)]
        public string ContentType { get; set; } = string.Empty;

        [Required(ErrorMessage = ErrorCodes.ValidationRequiredField)]
        [Range(1, 10485760, ErrorMessage = ErrorCodes.ValidationFileSizeExceeded)] // Max 10MB
        public long FileSize { get; set; }
    }
}
