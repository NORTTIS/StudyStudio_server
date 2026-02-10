using StudioStudio_Server.Models.Enums;

namespace StudioStudio_Server.Models.DTOs.Request
{
    public class ResendEmailRequest
    {
        public string Email { get; set; } = null!;
        public EmailTokenType Purpose { get; set; }
    }
}
