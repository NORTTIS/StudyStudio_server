namespace StudioStudio_Server.Models.DTOs.Response
{
    public class GroupListResponse
    {
        public SubscriptionInfo Subscription { get; set; } = null!;
        public GroupSummary Summary { get; set; } = null!;
        public GroupSections Sections { get; set; } = null!;
    }

    public class StudioGroupListResponse
    {
        public int TotalGroup { get; set; }
        public List<GroupCardDto> StudioGroups { get; set; } = new();
    }

    public class SubscriptionInfo
    {
        public int GroupLimit { get; set; }
        public int GroupCreated { get; set; }
        public int MemberLimit { get; set; }
    }

    public class GroupSummary
    {
        public int TotalGroups { get; set; }
        public int FavoriteCount { get; set; }
        public int StudioGroupCount { get; set; }
        public int IndependentGroupCount { get; set; }
        public int ArchivedCount { get; set; }
    }

    public class GroupSections
    {
        public List<GroupCardDto> Favorites { get; set; } = new();
        public List<GroupCardDto> StudioGroups { get; set; } = new();
        public List<GroupCardDto> IndependentGroups { get; set; } = new();
        public List<GroupCardDto> ArchivedGroups { get; set; } = new();
    }

    public class GroupCardDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public bool IsFavorite { get; set; }
        public string Role { get; set; } = string.Empty;
        public StudioDto? Studio { get; set; }
        public UserDto CreatedBy { get; set; } = null!;
        public int MemberCount { get; set; }
        public int TaskCount { get; set; }
        public DateTime LastActivityAt { get; set; }
        public List<MemberPreviewDto> MembersPreview { get; set; } = new();

        // 🔹 ADDED: Group personalization
        public string? AvatarUrl { get; set; }
        public string? ColorHex { get; set; }
        public string? IconEmoji { get; set; }
        public string? BannerUrl { get; set; }
        public string? Tagline { get; set; }
        public string? Alias { get; set; }
        public bool IsOpen { get; set; }
        public bool IsArchived { get; set; }
        public bool IsMember { get; set; }
    }

    public class StudioDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    public class UserDto
    {
        public Guid Id { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string? AvatarUrl { get; set; }
    }

    public class MemberPreviewDto
    {
        public Guid Id { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string? AvatarUrl { get; set; }
    }
}