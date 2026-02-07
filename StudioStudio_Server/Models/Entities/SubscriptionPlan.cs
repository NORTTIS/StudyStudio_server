namespace StudioStudio_Server.Models.Entities
{
    public class SubscriptionPlan
    {
        public Guid PlanId { get; set; }

        public string PlanName { get; set; } = null!;
        public decimal Price { get; set; }

        public BillingCycle BillingCycle { get; set; }
        public string Description { get; set; } = null!;

        public int MaxStudios { get; set; }
        public int MaxStorageMb { get; set; }

        public int MaxAiRequestsPerDay { get; set; }

        public bool IsActive { get; set; }
    }
}