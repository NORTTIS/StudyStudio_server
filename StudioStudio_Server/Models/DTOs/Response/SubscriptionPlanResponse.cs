namespace StudioStudio_Server.Models.DTOs.Response
{
    public class SubscriptionPlanResponse
    {
        public List<SubscriptionPlanItem> Plans { get; set; } = new List<SubscriptionPlanItem>();
    }

    public class SubscriptionPlanItem
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
    }
}
