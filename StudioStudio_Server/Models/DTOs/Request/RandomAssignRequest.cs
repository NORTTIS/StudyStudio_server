using System.ComponentModel.DataAnnotations;
using StudioStudio_Server.Exceptions;
using StudioStudio_Server.Models.Entities;

namespace StudioStudio_Server.Models.DTOs.Request
{
    /// <summary>
    /// Request to randomly assign studio members to groups
    /// </summary>
    public class RandomAssignRequest
    {
        /// <summary>
        /// Target group IDs. If empty/null, all studio groups are targeted
        /// </summary>
        public List<Guid>? TargetGroupIds { get; set; }

        /// <summary>
        /// User IDs to exclude from assignment (e.g., TAs)
        /// </summary>
        public List<Guid>? ExcludeUserIds { get; set; }

        /// <summary>
        /// Default role to assign. Cannot be Owner.
        /// Valid: Member, Commenter, Viewer, Moderator
        /// </summary>
        [Required(ErrorMessage = ErrorCodes.ValidationRequiredField)]
        [EnumDataType(typeof(GroupRole), ErrorMessage = ErrorCodes.ValidationInvalidRoleValue)]
        public GroupRole DefaultRole { get; set; } = GroupRole.Member;

        /// <summary>
        /// Scope of members to assign
        /// </summary>
        [Required(ErrorMessage = ErrorCodes.ValidationRequiredField)]
        public AssignScope Scope { get; set; } = AssignScope.Unassigned;

    }

    public enum AssignScope
    {
        Unassigned,
        All
    }
}
