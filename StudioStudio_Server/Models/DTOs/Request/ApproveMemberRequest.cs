using System.ComponentModel.DataAnnotations;

namespace StudioStudio_Server.Models.DTOs.Request
{
    public class ApproveMemberRequest
    {
        [Required]
        public Guid UserId { get; set; }
    }
}
