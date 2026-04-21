namespace StudioStudio_Server.Models.DTOs.Response
{
    /// <summary>
    /// Response for batch assign operation
    /// </summary>
    public class BatchAssignResponse
    {
        /// <summary>
        /// Total number of data rows in the uploaded file
        /// </summary>
        public int TotalRows { get; set; }

        /// <summary>
        /// Number of rows successfully processed (Added + Skipped + RoleUpdated)
        /// </summary>
        public int SuccessCount { get; set; }

        /// <summary>
        /// Number of rows skipped (member already in group with same role)
        /// </summary>
        public int SkippedCount { get; set; }

        /// <summary>
        /// List of error rows that could not be processed
        /// </summary>
        public List<BatchErrorRow> Errors { get; set; } = new();

        /// <summary>
        /// Summary of all assignments with their resulting actions
        /// </summary>
        public List<BatchAssignmentItem> Assignments { get; set; } = new();
    }

    /// <summary>
    /// A single row that could not be processed
    /// </summary>
    public class BatchErrorRow
    {
        /// <summary>
        /// 1-based row number in the file (after header)
        /// </summary>
        public int Row { get; set; }

        /// <summary>
        /// Email address from the problematic row
        /// </summary>
        public string? Email { get; set; }

        /// <summary>
        /// Group name from the problematic row
        /// </summary>
        public string? GroupName { get; set; }

        /// <summary>
        /// Error code describing the failure
        /// </summary>
        public string Reason { get; set; } = string.Empty;

        /// <summary>
        /// Localized error message
        /// </summary>
        public string Message { get; set; } = string.Empty;
    }

    /// <summary>
    /// A single assignment with its action result
    /// </summary>
    public class BatchAssignmentItem
    {
        /// <summary>
        /// Email of the member
        /// </summary>
        public string Email { get; set; } = string.Empty;

        /// <summary>
        /// Target group name
        /// </summary>
        public string GroupName { get; set; } = string.Empty;

        /// <summary>
        /// Role assigned
        /// </summary>
        public string Role { get; set; } = string.Empty;

        /// <summary>
        /// Action taken: Added, Skipped, RoleUpdated
        /// </summary>
        public string Action { get; set; } = string.Empty;
    }
}
