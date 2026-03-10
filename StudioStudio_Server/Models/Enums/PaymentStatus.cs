namespace StudioStudio_Server.Models.Enums
{
    /// <summary>
    /// Payment status enumeration
    /// Represents the current state of a payment transaction
    /// </summary>
    public enum PaymentStatus
    {
        /// <summary>
        /// Payment is pending and waiting for completion
        /// </summary>
        PENDING = 0,

        /// <summary>
        /// Payment completed successfully
        /// </summary>
        SUCCESS = 1,

        /// <summary>
        /// Payment was cancelled by user or system
        /// </summary>
        CANCELLED = 2,

        /// <summary>
        /// Payment failed due to error or rejection
        /// </summary>
        FAILED = 3
    }
}
