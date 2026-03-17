using StudioStudio_Server.Models.Enums;

namespace StudioStudio_Server.Models.DTOs.Response
{
    public class GroupInfoItem
    {
        public Guid GroupId { get; set; }
        public string GroupName { get; set; } = string.Empty;
        public GroupRole GroupRole { get; set; }
    }

    public class StudioMemberResponse
    {
        public Guid UserId { get; set; }
        public string UserName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? AvatarUrl { get; set; }
        public StudioRole StudioRole { get; set; }
        public List<GroupInfoItem> GroupInfo { get; set; } = new();
    }
}
