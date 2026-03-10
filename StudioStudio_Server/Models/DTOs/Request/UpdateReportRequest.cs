using System.ComponentModel.DataAnnotations;
using StudioStudio_Server.Exceptions;
using StudioStudio_Server.Models.Enums;

namespace StudioStudio_Server.Models.DTOs.Request
{
    public class UpdateReportRequest
    {
        [Required(ErrorMessage = ErrorCodes.ValidationRequiredField)]
        public Guid ReportId { get; set; }

        public ReportStatus? Status { get; set; }

        public ReportPriority? Priority { get; set; }

        [StringLength(500, ErrorMessage = ErrorCodes.ValidationRequiredField)]
        public string? AdminNote { get; set; }
    }
}
