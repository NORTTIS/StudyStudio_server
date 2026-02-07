using StudioStudio_Server.Models.Entities;

public class TaskItem
{
    public Guid TaskId { get; set; }

    public Guid? GroupId { get; set; }
    public Guid OwnerId { get; set; }

    public Guid? GroupStatusId { get; set; }
    public Guid? PersonalStatusId { get; set; }

    public string Title { get; set; } = null!;
    public string? Description { get; set; }

    public DateTime? DueDate { get; set; }
    public int Priority { get; set; }

    public bool IsPendingDeleted { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public Group? Group { get; set; }
    public GroupTaskStatus? GroupStatus { get; set; }
    public PersonalTaskStatus? PersonalStatus { get; set; }

    public User Owner { get; set; } = null!;
}
