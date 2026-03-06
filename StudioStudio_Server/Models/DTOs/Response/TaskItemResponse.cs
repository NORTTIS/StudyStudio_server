using StudioStudio_Server.Models.Entities;
using StudioStudio_Server.Models.Enums;

namespace StudioStudio_Server.Models.DTOs.Response
{
    public class TaskItemResponse
    {
        public Guid TaskId { get; set; }
        public string TaskTitle { get; set; } = string.Empty;
        public string TaskDescription { get; set; } = string.Empty;
        public TaskPriority TaskPriority { get; set; }
        public TaskSeverity TaskSeverity { get; set; }
        public int Position { get; set; }
        public int Progress { get; set; }
        public Guid CreatedById { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? DueDate { get; set; }
        public GroupTaskStatusDto? GroupStatus { get; set; }
        public PersonalTaskStatusDto? PersonalStatus { get; set; }
        public UserDto Assignee { get; set; } = new UserDto();

    }

    public class GroupTaskStatusDto
    {
        public Guid GroupId { get; set; }
        public string StatusName { get; set; } = null!;
        public int Position { get; set; }
    }

    public class PersonalTaskStatusDto
    {
        public Guid UserId { get; set; }

        public string StatusName { get; set; } = null!;

        public int Position { get; set; }
    }
}
