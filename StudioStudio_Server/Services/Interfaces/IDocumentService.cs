using StudioStudio_Server.Models.DTOs.Request;
using StudioStudio_Server.Models.DTOs.Response;

namespace StudioStudio_Server.Services.Interfaces
{
    /// <summary>
    /// Service interface cho Document upload và processing
    /// </summary>
    public interface IDocumentService
    {
        Task<RequestDocumentUploadResponse> RequestUploadAsync(Guid userId, RequestDocumentUploadRequest request);
        Task CompleteUploadAsync(Guid userId, Guid attachmentId);
        Task<DocumentStatusResponse> GetDocumentStatusAsync(Guid userId, Guid attachmentId);
        Task<GroupDocumentsResponse> GetGroupDocumentsAsync(Guid userId, Guid groupId);
        Task DeleteDocumentAsync(Guid userId, Guid attachmentId);
    }
}
