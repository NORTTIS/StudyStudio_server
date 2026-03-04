using StudioStudio_Server.Models.DTOs.Request;
using StudioStudio_Server.Models.DTOs.Response;
using StudioStudio_Server.Repositories.Interfaces;

namespace StudioStudio_Server.Services.Interfaces
{
    /// <summary>
    /// Service interface for Document upload and processing
    /// </summary>
    public interface IDocumentService
    {
        Task<RequestDocumentUploadResponse> RequestUploadAsync(Guid userId, RequestDocumentUploadRequest request);
        Task CompleteUploadAsync(Guid userId, Guid attachmentId);
        Task<DocumentStatusResponse> GetDocumentStatusAsync(Guid userId, Guid attachmentId);
        Task<GroupDocumentsResponse> GetGroupDocumentsAsync(Guid userId, Guid groupId);
        Task DeleteDocumentAsync(Guid userId, Guid attachmentId);
        Task<string> GetDocumentDownloadUrlAsync(Guid userId, Guid attachmentId, int expirationMinutes = 60);
        
        /// <summary>
        /// Process document in background: extract text, chunk, generate embeddings, upsert to Qdrant
        /// Called by EmbeddingBackgroundService
        /// </summary>
        Task ProcessDocumentAsync(
            Guid attachmentId,
            IGroupAttachmentRepository attachmentRepository,
            IFileStorageService fileStorageService,
            IEmbeddingService embeddingService,
            IVectorDatabaseService vectorDbService,
            ILogger logger);
    }
}
