using StudioStudio_Server.Models.DTOs.Request;
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
        Task<TaskCommentDto> SendCommentAsync(Guid userId, SendTaskCommentRequest request);
        Task<TaskCommentDto> ReplyToCommentAsync(Guid userId, ReplyToTaskCommentRequest request);
        Task DeleteCommentAsync(Guid userId, DeleteTaskCommentRequest request);
    }
}
