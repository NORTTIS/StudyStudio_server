namespace StudioStudio_Server.Repositories.Interfaces
{
    public interface IPersonalTaskStatusRepository
    {
        Task AddAsync(PersonalTaskStatus personalTaskStatus);
        Task<List<PersonalTaskStatus>> GetAllByUserIdAsync(Guid userId);
        Task DeletePersonalStatusAsync(PersonalTaskStatus status);
        Task UpdatePersonalStatusAsync(PersonalTaskStatus status);
        Task AddPersonalStatusAsync(PersonalTaskStatus status);
        Task SaveChangesAsync();
        Task ReorderStatusAsync(Guid statusId, Guid? prevStatusId, Guid? nextStatusId, Guid userId);
        Task<PersonalTaskStatus?> FindNextAfterAsync(Guid userId, long position);
        Task RebalanceColumnAsync(Guid userId);
        Task<bool> IsNameExist(PersonalTaskStatus status);
        Task<PersonalTaskStatus?> GetDetailAsync(Guid statusId);
    }
}
