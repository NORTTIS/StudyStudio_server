namespace StudioStudio_Server.Models.DTOs.Response
{
    /// <summary>
    /// Response cho AI question
    /// Hybrid RAG: Document context + Task statistics
    /// </summary>
    public class AIAnswerResponse
    {
        public string Answer { get; set; } = string.Empty;
        public List<SourceDocument> SourceDocuments { get; set; } = new();
        public TaskSummaryResponse? TaskSummary { get; set; }
        public long ProcessingTimeMs { get; set; }
        public DateTime GeneratedAt { get; set; }
    }

    public class SourceDocument
    {
        public string? DocumentId { get; set; }
        public int ChunkIndex { get; set; }
        public float RelevanceScore { get; set; }
        public string? Preview { get; set; }
    }

    public class TaskSummaryResponse
    {
        public int TotalTasks { get; set; }
        public int CompletedTasks { get; set; }
        public int CompletionPercentage { get; set; }
        public int OverdueTasks { get; set; }
        public DateTime? NearestDeadline { get; set; }
        public List<string> RiskFlags { get; set; } = new();
    }
}
