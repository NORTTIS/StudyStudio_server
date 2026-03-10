namespace StudioStudio_Server.Models.DTOs.Response
{
    /// <summary>
    /// Response for subscription plan statistics
    /// Includes total users, free users, premium users, conversion rate, and plan details
    /// </summary>
    public class SubscriptionStatisticsResponse
    {
        public UserStatistics UserStats { get; set; } = new UserStatistics();
        public List<SubscriptionPlanDetail> Plans { get; set; } = new List<SubscriptionPlanDetail>();
    }

    public class UserStatistics
    {
        public int TotalActiveUsers { get; set; }
        public int FreeUsers { get; set; }
        public int PremiumUsers { get; set; }
        public decimal ConversionRate { get; set; }
    }

    public class SubscriptionPlanDetail
    {
        public Guid PlanId { get; set; }
        public string PlanName { get; set; } = null!;
        public decimal Price { get; set; }
        public BillingCycle BillingCycle { get; set; }
        public string Description { get; set; } = null!;
        public int MaxStudios { get; set; }
        public int MaxStorageMb { get; set; }
        public int MaxAiRequestsPerDay { get; set; }
        public int MaxGroups { get; set; }
        public int MaxMembersPerGroup { get; set; }
        public bool IsActive { get; set; }
        public int SubscriberCount { get; set; }
    }
}
