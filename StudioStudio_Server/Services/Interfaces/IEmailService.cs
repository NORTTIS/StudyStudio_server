namespace StudioStudio_Server.Services.Interfaces
{
    public interface IEmailService
    {
        Task SendLinkAsync(string to, string subject, string body);
    }
}
