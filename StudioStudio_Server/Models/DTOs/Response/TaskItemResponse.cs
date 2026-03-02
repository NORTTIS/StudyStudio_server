using StudioStudio_Server.Models.Entities;

namespace StudioStudio_Server.Models.DTOs.Response
{
    public class TaskItemResponse
    {
        Guid TaskId { get; set; }
        public string TaskName { get; set; } = string.Empty;
        public string TaskDescription { get; set; } = string.Empty;
        public string TaskStatus { get; set; } = string.Empty;
        public int TaskPriority { get; set; }
        public int TaskSeverity { get; set; }
        public int Position { get; set; }
        public Guid CreatedById { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime DueDate { get; set; }
        public GroupTaskStatusDto? GroupStatus { get; set; }
        public PersonalTaskStatusDto? PersonalStatus { get; set; }
        public List<UserDto> Assignee { get; set; } = new List<UserDto>();

    }

    public class GroupTaskStatusDto
    {

    }

    public class PersonalTaskStatusDto
    {

    }
}
