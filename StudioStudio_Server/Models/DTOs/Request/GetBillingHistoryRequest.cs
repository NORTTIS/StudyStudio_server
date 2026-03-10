using System.ComponentModel.DataAnnotations;
using StudioStudio_Server.Models.Enums;

namespace StudioStudio_Server.Models.DTOs.Request
{
    /// <summary>
    /// Request DTO for getting billing history (admin)
    /// </summary>
    public class GetBillingHistoryRequest
    {
        /// <summary>
        /// Search term - matches userName, userEmail, or invoiceId (OrderCode)
        /// </summary>
        public string? SearchTerm { get; set; }

        /// <summary>
        /// Filter by payment status: PENDING, SUCCESS, CANCELLED, FAILED
        /// </summary>
        public PaymentStatus? PaymentStatus { get; set; }

        /// <summary>
        /// Page number (1-based)
        /// </summary>
        [Range(1, int.MaxValue)]
        public int PageNumber { get; set; } = 1;

        /// <summary>
        /// Page size
        /// </summary>
        [Range(1, 100)]
        public int PageSize { get; set; } = 10;
    }
}
