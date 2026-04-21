using System.ComponentModel.DataAnnotations;

namespace StudioStudio_Server.Models.DTOs.Request
{
    public class UpdateGroupRequest
    {
        [Required]
        public Guid GroupId { get; set; }

        [Required]
        [StringLength(100, MinimumLength = 1)]
        public string GroupName { get; set; } = string.Empty;

        [StringLength(500)]
        public string? Description { get; set; }

        public bool IsTemplate { get; set; } = false;

        public string? AvatarUrl { get; set; }
        public string? ColorHex { get; set; }
        public string? IconEmoji { get; set; }
        public string? BannerUrl { get; set; }
        public string? Tagline { get; set; }
        public string? Alias { get; set; }
        public bool? IsOpen { get; set; }
        public bool? AllowMemberUpdateProgress { get; set; }
    }
}
