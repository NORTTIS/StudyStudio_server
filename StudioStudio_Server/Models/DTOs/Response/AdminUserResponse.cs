namespace StudioStudio_Server.Models.DTOs.Response
{
    /// <summary>
    /// User list item for admin dashboard
    /// </summary>
    public class UserListItem
    {
        public Guid UserId { get; set; }
        public string Email { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Package { get; set; } = string.Empty;
        public int GroupCount { get; set; }
        public int StudioCount { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? LastLoginAt { get; set; }
        public string Status { get; set; } = string.Empty;
    }

    /// <summary>
    /// Detailed user information for admin
    /// </summary>
    public class UserDetailItem : UserListItem
    {
        public string? PhoneNumber { get; set; }
        public string? Bio { get; set; }
        public string? AvatarUrl { get; set; }
        public bool IsVerify { get; set; }
        public DateTime UpdatedAt { get; set; }
        public bool IsAdmin { get; set; }

        // Subscription details
        public SubscriptionPlanInfo? Subscription { get; set; }
    }

    /// <summary>
    /// Subscription plan information
    /// </summary>
    public class SubscriptionPlanInfo
    {
        public Guid PlanId { get; set; }
        public string PlanName { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public string BillingCycle { get; set; } = string.Empty;
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public bool IsActive { get; set; }
    }

    /// <summary>
    /// User list summary statistics
    /// </summary>
    public class UserListSummary
    {
        public int TotalUsers { get; set; }
        public int ActiveUsers { get; set; }
        public int InactiveUsers { get; set; }
        public int DeletedUsers { get; set; }
        public int PremiumUsers { get; set; }
        public int FreeUsers { get; set; }
    }

    /// <summary>
    /// Paginated user list response
    /// </summary>
    public class UserListResponse
    {
        public UserListSummary Summary { get; set; } = new UserListSummary();
        public List<UserListItem> UserList { get; set; } = new List<UserListItem>();
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
        public int TotalCount { get; set; }
        public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
    }
}
