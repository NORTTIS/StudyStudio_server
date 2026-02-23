using System.ComponentModel.DataAnnotations;

namespace StudioStudio_Server.Models.DTOs.Request
{
    public class AssignRoleRequest
    {
        [Required]
        public Guid GroupId { get; set; }

        [Required]
        public Guid UserId { get; set; }

        [Required]
        public string Role { get; set; } = string.Empty;
    }
}
