using StudioStudio_Server.Models.DTOs.Response;

namespace StudioStudio_Server.Services.Interfaces
{
    /// <summary>
    /// Service interface cho Task Comments (l?y l?ch s? comments)
    /// Note: Realtime commenting ðý?c handle b?i TaskCommentHub (SignalR)
    /// </summary>
    public interface ITaskCommentService
    {
        Task<TaskCommentListResponse> GetTaskCommentsAsync(Guid userId, Guid taskId, int limit, int offset);
    }
}
