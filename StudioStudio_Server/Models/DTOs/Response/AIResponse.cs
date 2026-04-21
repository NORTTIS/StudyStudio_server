namespace StudioStudio_Server.Models.DTOs.Response
{
    public class TaskSummaryResponse
    {
        public int TotalTasks { get; set; }
        public int CompletedTasks { get; set; }
        public int InProgressTasks { get; set; }
        public int NotStartedTasks { get; set; }
        public int CompletionPercentage { get; set; }
        public int OverdueTasks { get; set; }
        public DateTime? NearestDeadline { get; set; }
        public List<string> RiskFlags { get; set; } = new();
        // Priority breakdown
        public int HighPriorityTasks { get; set; }
        public int MediumPriorityTasks { get; set; }
        public int LowPriorityTasks { get; set; }
        // Severity breakdown
        public int CriticalSeverityTasks { get; set; }
        public int MajorSeverityTasks { get; set; }
        public int ModerateSeverityTasks { get; set; }
        public int MinorSeverityTasks { get; set; }
    }
}
