using StudioStudio_Server.Models.Entities;

namespace StudioStudio_Server.Repositories.Interfaces
{
    public interface IGroupMessageRepository
    {
        Task<GroupMessage> AddAsync(GroupMessage message);
        Task<GroupMessage?> GetByIdAsync(Guid messageId);
        Task<GroupMessage?> GetByIdWithRepliesAsync(Guid messageId);
        Task<List<GroupMessage>> GetByGroupIdAsync(Guid groupId, int limit = 100, int offset = 0);
        Task<int> GetCountByGroupIdAsync(Guid groupId);
        Task<int> GetReplyCountAsync(Guid messageId);
        Task SoftDeleteWithRepliesAsync(Guid messageId);
    }
}
