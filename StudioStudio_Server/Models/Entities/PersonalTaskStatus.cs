using StudioStudio_Server.Models.Entities;

public class PersonalTaskStatus
{
    public Guid StatusId { get; set; }

    public Guid UserId { get; set; }

    public string StatusName { get; set; } = null!;

    public int Position { get; set; }

    public DateTime CreatedAt { get; set; }

    public User User { get; set; } = null!;
}
