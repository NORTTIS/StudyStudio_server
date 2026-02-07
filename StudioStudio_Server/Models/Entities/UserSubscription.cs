namespace StudioStudio_Server.Models.Entities
{
    public class UserSubscription
    {
        public Guid SubscriptionId { get; set; }

        public Guid UserId { get; set; }
        public Guid PlanId { get; set; }

        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }

        public bool IsActive { get; set; }

        public User User { get; set; } = null!;
        public SubscriptionPlan Plan { get; set; } = null!;
    }
}