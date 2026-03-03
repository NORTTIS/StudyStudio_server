using StudioStudio_Server.Models.DTOs.Response;

namespace StudioStudio_Server.Services.Interfaces
{
    /// <summary>
    /// Interface for vector database operations (Qdrant)
    /// Implementations: QdrantService
    /// </summary>
    public interface IVectorDatabaseService
    {
        /// <summary>
        /// Add vector to collection
        /// </summary>
        /// <param name="id">Unique ID for vector</param>
        /// <param name="vector">Vector embeddings (float array)</param>
        /// <param name="payload">Metadata (JSON object)</param>
        /// <returns>True if successful</returns>
        Task<bool> UpsertVectorAsync(string id, float[] vector, Dictionary<string, object> payload);

        /// <summary>
        /// Search for similar vectors
        /// </summary>
        /// <param name="queryVector">Query vector for search</param>
        /// <param name="limit">Number of results (default: 5)</param>
        /// <param name="filters">Filters for payload (optional)</param>
        /// <returns>List of similar vectors with scores</returns>
        Task<List<VectorSearchResult>> SearchSimilarAsync(float[] queryVector, int limit = 5, Dictionary<string, object>? filters = null);

        /// <summary>
        /// Search vectors with groupId filter (for AI Q&A)
        /// </summary>
        /// <param name="queryVector">Query vector for search</param>
        /// <param name="topK">Number of results to return</param>
        /// <param name="groupId">Group ID to filter</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>List of search results</returns>
        Task<List<VectorSearchResponse.SearchResult>> SearchVectorsAsync(
            float[] queryVector,
            int topK,
            Guid groupId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Delete vector from collection
        /// </summary>
        /// <param name="id">ID of vector to delete</param>
        /// <returns>True if delete successful</returns>
        Task<bool> DeleteVectorAsync(string id);

        /// <summary>
        /// Delete multiple vectors by filter (Qdrant DSL format)
        /// </summary>
        /// <param name="filters">Filters to identify vectors to delete</param>
        /// <returns>True if delete successful</returns>
        Task<bool> DeleteVectorsByFilterAsync(Dictionary<string, object> filters);

        /// <summary>
        /// Delete all vectors belonging to a group
        /// </summary>
        /// <param name="groupId">Group ID</param>
        /// <returns>True if delete successful</returns>
        Task<bool> DeleteVectorsByGroupIdAsync(Guid groupId);

        /// <summary>
        /// Delete all vectors of a user
        /// </summary>
        /// <param name="userId">User ID</param>
        /// <returns>True if delete successful</returns>
        Task<bool> DeleteVectorsByUserIdAsync(Guid userId);

        /// <summary>
        /// Delete vectors of user NOT belonging to specific group
        /// </summary>
        /// <param name="userId">User ID</param>
        /// <param name="groupId">Group ID (exclude)</param>
        /// <returns>True if delete successful</returns>
        Task<bool> DeleteVectorsByUserNotInGroupAsync(Guid userId, Guid groupId);

        /// <summary>
        /// Delete all vectors of a specific document
        /// </summary>
        /// <param name="documentId">Document ID</param>
        /// <returns>True if delete successful</returns>
        Task<bool> DeleteVectorsByDocumentIdAsync(Guid documentId);

        /// <summary>
        /// Get vector information by ID
        /// </summary>
        /// <param name="id">ID of vector</param>
        /// <returns>Vector data or null if not found</returns>
        Task<VectorSearchResult?> GetVectorByIdAsync(string id);
    }

    /// <summary>
    /// Vector search result
    /// </summary>
    public class VectorSearchResult
    {
        public string Id { get; set; } = string.Empty;
        public float Score { get; set; }
        public Dictionary<string, object> Payload { get; set; } = new();
        public float[]? Vector { get; set; }
    }
}
