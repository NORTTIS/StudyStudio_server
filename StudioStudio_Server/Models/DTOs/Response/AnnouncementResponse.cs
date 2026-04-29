namespace StudioStudio_Server.Models.DTOs.Response
{
    public class AnnouncementResponse
    {
        public Guid AnnouncementId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? PublishedAt { get; set; }
        public bool IsRead { get; set; }
        public Guid? TaskId { get; set; }
        public Guid? GroupId { get; set; }
        public string? SourceType { get; set; }
    }

    public class AnnouncementListResponse
    {
        public List<AnnouncementResponse> Items { get; set; } = new();
        public int TotalCount { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalPages { get; set; }
        public bool HasMore => Page < TotalPages;
    }
}
