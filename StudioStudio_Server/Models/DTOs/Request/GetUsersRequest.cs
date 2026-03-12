using StudioStudio_Server.Models.Enums;

namespace StudioStudio_Server.Models.DTOs.Request
{
    public class GetUsersRequest
    {
        /// <summary>
        /// Search term for filtering by name or email
        /// </summary>
        public string? SearchTerm { get; set; }

        /// <summary>
        /// Filter by user status: Active, Inactive, Deleted
        /// </summary>
        public UserStatus? Status { get; set; }

        /// <summary>
        /// Filter by package: "Free" or "Premium"
        /// </summary>
        public string? Package { get; set; }

        /// <summary>
        /// Page number (default: 1)
        /// </summary>
        public int PageNumber { get; set; } = 1;

        /// <summary>
        /// Page size (default: 10)
        /// </summary>
        public int PageSize { get; set; } = 10;
    }
}
