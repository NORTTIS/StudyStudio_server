namespace StudioStudio_Server.Repositories.Interfaces
{
    public interface IPersonalTaskStatusRepository
    {
        Task AddAsync(PersonalTaskStatus personalTaskStatus);
    }
}
