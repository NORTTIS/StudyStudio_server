namespace StudioStudio_Server.Models.DTOs.Response
{
    /// <summary>
    /// Response for random assign operation
    /// </summary>
    public class RandomAssignResponse
    {
        /// <summary>
        /// Total members assigned
        /// </summary>
        public int AssignedCount { get; set; }

        /// <summary>
        /// List of groups with their new member assignments
        /// </summary>
        public List<GroupAssignmentSummary> Groups { get; set; } = new();

        /// <summary>
        /// Groups that had conflicts (e.g., already have moderator)
        /// Only populated when the operation failed due to conflicts
        /// </summary>
        public List<GroupConflictInfo>? Conflicts { get; set; }

        /// <summary>
        /// Whether the operation succeeded
        /// </summary>
        public bool Success { get; set; }
    }

    /// <summary>
    /// Summary of members assigned to a group
    /// </summary>
    public class GroupAssignmentSummary
    {
        public Guid GroupId { get; set; }
        public string GroupName { get; set; } = string.Empty;
        public int MemberCount { get; set; }
        public List<MemberAssignmentDetail> Members { get; set; } = new();
    }

    /// <summary>
    /// Detail of a single member assignment
    /// </summary>
    public class MemberAssignmentDetail
    {
        public Guid UserId { get; set; }
        public string Email { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
    }

    /// <summary>
    /// Conflict information for a group that cannot proceed
    /// </summary>
    public class GroupConflictInfo
    {
        public Guid GroupId { get; set; }
        public string GroupName { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
    }
}
