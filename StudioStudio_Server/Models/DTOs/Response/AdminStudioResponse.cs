namespace StudioStudio_Server.Models.DTOs.Response
{
    public class StudioListItem
    {
        public Guid StudioId { get; set; }
        public string StudioName { get; set; } = null!;
        public string? Description { get; set; }
        public string OwnerName { get; set; } = null!;
        public string OwnerEmail { get; set; } = null!;
        public int GroupCount { get; set; }
        public int MemberCount { get; set; }
        public int TaskCount { get; set; }
        public DateTime CreatedAt { get; set; }

        public DateTime? LastActivityAt { get; set; }
        public bool IsActive { get; set; }
    }
    public class StudioListSummary
    {
        public int TotalStudios { get; set; }

        public int ActiveStudios { get; set; }

        public int InactiveStudios { get; set; }

        public int TotalMembers { get; set; }

        public int TotalGroups { get; set; }
    }

    public class AdminStudioListResponse
    {
        public StudioListSummary Summary { get; set; } = null!;

        public List<StudioListItem> StudioList { get; set; } = new();

        public int PageNumber { get; set; }

        public int PageSize { get; set; }

        public int TotalCount { get; set; }

        public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
    }
}
