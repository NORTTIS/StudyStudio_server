namespace StudioStudio_Server.Models.DTOs.Response
{
    /// <summary>
    /// Response cho /api/analytics/group/{groupId}/summary
    /// Trả về tổng quan không có date filter - dùng cho Chart 1, 2, 4, 6
    /// </summary>
    public class GroupSummaryResponse
    {
        /// <summary>
        /// Task status breakdown per member (done, in-progress, todo, overdue)
        /// Dùng cho: Chart 1 (Personal Donut), Chart 4 (Bar Chart)
        /// </summary>
        public List<MemberTaskBreakdownData> MemberTaskBreakdown { get; set; } = new();

        /// <summary>
        /// Unique task breakdown for entire group (for Team Chart)
        /// </summary>
        public GroupTaskBreakdownData? GroupTaskBreakdown { get; set; }

        /// <summary>
        /// Member activity summary với last activity timestamp
        /// Dùng cho: Chart 6 (Member Progress Cards)
        /// </summary>
        public List<MemberActivitySummary> MemberActivitySummary { get; set; } = new();

        /// <summary>
        /// Member contribution data (tasks completed, created, messages)
        /// Dùng cho: Layer chi tiết khi click vào member
        /// </summary>
        public List<MemberContributionData> MemberContribution { get; set; } = new();
    }

    /// <summary>
    /// Unique task breakdown for entire group (for Team Chart)
    /// </summary>
    public class GroupTaskBreakdownData
    {
        /// <summary>
        /// Unique total count — each task counted once (Venn overlaps excluded)
        /// </summary>
        public int TotalTasks { get; set; }
        public int TodoTasks { get; set; }
        public int InProgressTasks { get; set; }
        public int DoneTasks { get; set; }
        public int OverdueTasks { get; set; }
        public int InProgressOverdueTasks { get; set; }
        public int TodoOverdueTasks { get; set; }
    }
}
