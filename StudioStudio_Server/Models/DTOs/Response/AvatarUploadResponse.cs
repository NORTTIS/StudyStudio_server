namespace StudioStudio_Server.Models.DTOs.Response
{
    public class AvatarUploadResponse
    {
        public Guid EntityId { get; set; }
        public string UploadUrl { get; set; } = null!;
        public string FileKey { get; set; } = null!;
        public int ExpiresIn { get; set; }
    }
}
