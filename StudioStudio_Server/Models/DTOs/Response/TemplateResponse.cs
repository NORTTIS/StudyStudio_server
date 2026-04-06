namespace StudioStudio_Server.Models.DTOs.Response
{
    public class TemplateResponse
    {
        public Guid TemplateId { get; set; }
        public Guid UserId { get; set; }
        public Guid GroupId { get; set; }
        public string GroupName { get; set; } = string.Empty;
        public string? GroupDescription { get; set; }
        public bool IsSystemTemplate { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public List<GroupTaskStatusResponse> GroupTaskStatuses { get; set; } = new();

        // Group personalization fields (relevant for templates)
        public string? BannerUrl { get; set; }
        public string? ColorHex { get; set; }
    }

    /// <summary>
    /// Response for template list endpoint including subscription info
    /// </summary>
    public class TemplateListResponse
    {
        /// <summary>
        /// Subscription info showing user's group creation limits
        /// </summary>
        public SubscriptionQuota Subscription { get; set; } = null!;

        /// <summary>
        /// List of available templates (system + user's own)
        /// </summary>
        public List<TemplateResponse> Templates { get; set; } = new();
    }

    /// <summary>
    /// Subscription quota information for user
    /// </summary>
    public class SubscriptionQuota
    {
        /// <summary>
        /// Maximum groups user can create based on subscription plan
        /// </summary>
        public int GroupLimit { get; set; }

        /// <summary>
        /// Number of groups user has already created
        /// </summary>
        public int GroupCreated { get; set; }

        /// <summary>
        /// Maximum members allowed per group
        /// </summary>
        public int MemberLimit { get; set; }
    }
}
