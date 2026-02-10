using Microsoft.Extensions.Options;
using StudioStudio_Server.Configurations;
using StudioStudio_Server.Services.Interfaces;
using System.Net;
using System.Net.Mail;

namespace StudioStudio_Server.Services
{
    public class SMTPEmailService : IEmailService
    {
        private readonly EmailOptions _emailOptions;
        private readonly ILogger<SMTPEmailService> _logger;

        public SMTPEmailService(IOptions<EmailOptions> emailOptions, ILogger<SMTPEmailService> logger)
        {
            _emailOptions = emailOptions.Value;
            _logger = logger;
        }
        
        public async Task SendLinkAsync(string to, string subject, string body)
        {
            // Skip sending email if SMTP is not configured
            if (string.IsNullOrEmpty(_emailOptions.Host) || 
                string.IsNullOrEmpty(_emailOptions.From))
            {
                _logger.LogWarning("Email service is not configured. Skipping email to {To} with subject: {Subject}", to, subject);
                _logger.LogInformation("Email content (dev only): {Body}", body);
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
            _logger.LogInformation("Email sent successfully to {To}", to);
        }
    }
}
