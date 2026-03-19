using StudioStudio_Server.Models.DTOs.Request;
using StudioStudio_Server.Models.DTOs.Response;

namespace StudioStudio_Server.Services.Interfaces
{
    /// <summary>
    /// Service for batch member assignment to groups
    /// </summary>
    public interface IBatchAssignService
    {
        /// <summary>
        /// Process batch assignment from CSV/Excel file
        /// </summary>
        /// <param name="studioId">Studio ID</param>
        /// <param name="userId">User ID (must be studio owner)</param>
        /// <param name="stream">File stream</param>
        /// <param name="fileName">Original file name</param>
        /// <param name="cancellationToken">Cancellation token</param>
        Task<BatchAssignResponse> BatchAssignAsync(
            Guid studioId,
            Guid userId,
            Stream stream,
            string fileName,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Randomly assign studio members to groups
        /// </summary>
        /// <param name="studioId">Studio ID</param>
        /// <param name="userId">User ID (must be studio owner)</param>
        /// <param name="request">Random assign parameters</param>
        /// <param name="cancellationToken">Cancellation token</param>
        Task<RandomAssignResponse> RandomAssignAsync(
            Guid studioId,
            Guid userId,
            RandomAssignRequest request,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Generate a pre-filled CSV template for batch assignment
        /// </summary>
        /// <param name="studioId">Studio ID</param>
        /// <param name="userId">User ID (must be studio owner)</param>
        Task<byte[]> GenerateTemplateAsync(Guid studioId, Guid userId);
    }
}
