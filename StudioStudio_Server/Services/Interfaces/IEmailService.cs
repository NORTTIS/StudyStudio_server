namespace StudioStudio_Server.Services.Interfaces
{
    public interface IEmailService
    {
        /// <summary>
        /// Send email without checking user notification preference
        /// Use for: auth emails (verify, reset) where user hasn't set preferences yet
        /// </summary>
        Task SendLinkAsync(string to, string subject, string body);

        /// <summary>
        /// Send email with user notification preference check
        /// Use for: task notifications, mentions, reminders where user preference should be respected
        /// </summary>
        /// <param name="to">Recipient email address</param>
        /// <param name="subject">Email subject</param>
        /// <param name="body">Email body (HTML)</param>
        /// <param name="userId">User ID to check EmailNotificationEnabled flag</param>
        /// <returns>True if email was sent, false if skipped due to user preference</returns>
        Task<bool> SendEmailWithPreferenceCheckAsync(string to, string subject, string body, Guid userId);

        /// <summary>
        /// Send email with user object preloaded by caller to avoid duplicate user query
        /// </summary>
        /// <param name="user">Preloaded user entity for preference check</param>
        Task<bool> SendEmailWithPreferenceCheckAsync(string to, string subject, string body, Models.Entities.User user);
    }
}
