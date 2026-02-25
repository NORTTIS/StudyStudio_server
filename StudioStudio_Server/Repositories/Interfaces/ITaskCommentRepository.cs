using StudioStudio_Server.Models.Entities;

namespace StudioStudio_Server.Repositories.Interfaces
{
    public interface ITaskCommentRepository
    {
        Task<TaskComment> AddAsync(TaskComment comment);
        Task<TaskComment?> GetByIdAsync(Guid commentId);
        Task<TaskComment?> GetByIdWithRepliesAsync(Guid commentId);
        Task<List<TaskComment>> GetByTaskIdAsync(Guid taskId, int limit = 100, int offset = 0);
        Task<int> GetCountByTaskIdAsync(Guid taskId);
        Task<int> GetReplyCountAsync(Guid commentId);
        Task SoftDeleteWithRepliesAsync(Guid commentId);
    }
}
