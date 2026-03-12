using StudioStudio_Server.Exceptions;
using System.ComponentModel.DataAnnotations;

namespace StudioStudio_Server.Models.DTOs.Request
{
    /// <summary>
    /// Request for updating subscription plan information
    /// </summary>
    public class UpdateSubscriptionPlanRequest
    {
        [Required(ErrorMessage = ErrorCodes.ValidationRequiredField)]
        public Guid PlanId { get; set; }

        [Required(ErrorMessage = ErrorCodes.ValidationRequiredField)]
        [StringLength(100, ErrorMessage = ErrorCodes.ValidationStringLength)]
        public string PlanName { get; set; } = null!;

        [Required(ErrorMessage = ErrorCodes.ValidationRequiredField)]
        [Range(0, double.MaxValue, ErrorMessage = ErrorCodes.ValidationInvalidRange)]
        public decimal Price { get; set; }

        [Required(ErrorMessage = ErrorCodes.ValidationRequiredField)]
        public BillingCycle BillingCycle { get; set; }

        [Required(ErrorMessage = ErrorCodes.ValidationRequiredField)]
        [StringLength(500, ErrorMessage = ErrorCodes.ValidationStringLength)]
        public string Description { get; set; } = null!;

        [Required(ErrorMessage = ErrorCodes.ValidationRequiredField)]
        [Range(1, int.MaxValue, ErrorMessage = ErrorCodes.ValidationInvalidRange)]
        public int MaxStudios { get; set; }

        [Required(ErrorMessage = ErrorCodes.ValidationRequiredField)]
        [Range(1, int.MaxValue, ErrorMessage = ErrorCodes.ValidationInvalidRange)]
        public int MaxStorageMb { get; set; }

        [Required(ErrorMessage = ErrorCodes.ValidationRequiredField)]
        [Range(0, int.MaxValue, ErrorMessage = ErrorCodes.ValidationInvalidRange)]
        public int MaxAiRequestsPerDay { get; set; }

        [Required(ErrorMessage = ErrorCodes.ValidationRequiredField)]
        [Range(1, int.MaxValue, ErrorMessage = ErrorCodes.ValidationInvalidRange)]
        public int MaxGroups { get; set; }

        [Required(ErrorMessage = ErrorCodes.ValidationRequiredField)]
        [Range(1, int.MaxValue, ErrorMessage = ErrorCodes.ValidationInvalidRange)]
        public int MaxMembersPerGroup { get; set; }

        [Required(ErrorMessage = ErrorCodes.ValidationRequiredField)]
        public bool IsActive { get; set; }
    }
}
