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

        public SMTPEmailService(IOptions<EmailOptions> emailOptions)
        {
            _emailOptions = emailOptions.Value;
        }
        public async Task SendLinkAsync(string to, string subject, string body)
        {
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
        }
    }
}
