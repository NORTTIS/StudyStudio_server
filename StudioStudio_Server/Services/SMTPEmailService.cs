using Microsoft.Extensions.Options;
using StudioStudio_Server.Configurations;
using StudioStudio_Server.Repositories.Interfaces;
using StudioStudio_Server.Services.Interfaces;
using System.Net;
using System.Net.Mail;

namespace StudioStudio_Server.Services
{
    public class SMTPEmailService(
        IOptions<EmailOptions> emailOptions,
        IUserRepository userRepository,
        ILogger<SMTPEmailService> logger) : IEmailService
    {
        private readonly EmailOptions _emailOptions = emailOptions.Value;

        public async Task SendLinkAsync(string to, string subject, string body)
        {
            // Skip sending email if SMTP is not configured
            if (string.IsNullOrEmpty(_emailOptions.Host) ||
                string.IsNullOrEmpty(_emailOptions.From))
            {
                logger.LogWarning("Email service is not configured. Skipping email to {To} with subject: {Subject}", to, subject);
                logger.LogInformation("Email content (dev only): {Body}", body);
                return;
            }

            var message = new MailMessage
            {
                From = new MailAddress(_emailOptions.From),
                Subject = subject,
                Body = body,
                IsBodyHtml = true
            };

            message.To.Add(to);

            using var smtp = new SmtpClient(_emailOptions.Host, _emailOptions.Port)
            {
                Credentials = new NetworkCredential(
                    _emailOptions.Username,
                    _emailOptions.Password),
                EnableSsl = true
            };

            await smtp.SendMailAsync(message);
            logger.LogInformation("Email sent successfully to {To}", to);
        }

        public async Task<bool> SendEmailWithPreferenceCheckAsync(string to, string subject, string body, Guid userId)
        {
            // Check user's email notification preference
            var user = await userRepository.GetByIdAsync(userId);
            if (user != null && !user.EmailNotificationEnabled)
            {
                logger.LogInformation(
                    "Skipping email to {To} because user {UserId} has email notifications disabled",
                    to, userId);
                return false;
            }

            // User has notifications enabled or user not found (default to sending)
            try
            {
                await SendLinkAsync(to, subject, body);
                return true;
            }
            catch (SmtpException ex)
            {
                logger.LogWarning(ex,
                    "SMTP send failed for preferred email (userId: {UserId}, to: {To}, subject: {Subject}). Continuing without failing request.",
                    userId,
                    to,
                    subject);
                return false;
            }
            catch (Exception ex)
            {
                logger.LogError(ex,
                    "Unexpected email send failure (userId: {UserId}, to: {To}, subject: {Subject}). Continuing without failing request.",
                    userId,
                    to,
                    subject);
                return false;
            }
        }

        public async Task<bool> SendEmailWithPreferenceCheckAsync(string to, string subject, string body, Models.Entities.User user)
        {
            if (!user.EmailNotificationEnabled)
            {
                logger.LogInformation(
                    "Skipping email to {To} because user {UserId} has email notifications disabled",
                    to, user.UserId);
                return false;
            }

            try
            {
                await SendLinkAsync(to, subject, body);
                return true;
            }
            catch (SmtpException ex)
            {
                logger.LogWarning(ex,
                    "SMTP send failed for preferred email (userId: {UserId}, to: {To}, subject: {Subject}). Continuing without failing request.",
                    user.UserId,
                    to,
                    subject);
                return false;
            }
            catch (Exception ex)
            {
                logger.LogError(ex,
                    "Unexpected email send failure (userId: {UserId}, to: {To}, subject: {Subject}). Continuing without failing request.",
                    user.UserId,
                    to,
                    subject);
                return false;
            }
        }
    }
}
