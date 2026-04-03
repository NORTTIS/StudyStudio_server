using System.ComponentModel.DataAnnotations;

namespace StudioStudio_Server.Models.DTOs.Request
{
    public class RemoveMemberRequest
    {
        [Required]
        public Guid GroupId { get; set; }

        [Required]
        public Guid UserId { get; set; }
    }

    public class RemoveStudioMemberRequest
    {
        [Required]
        public Guid StudioId { get; set; }

        [Required]
        public Guid UserId { get; set; }
    }
}
