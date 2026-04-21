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
        /// Search vectors with groupId filter (for AI Q&A)
        /// </summary>
        /// <param name="queryVector">Query vector for search</param>
        /// <param name="topK">Number of results to return</param>
        /// <param name="groupId">Group ID to filter</param>
        /// <param name="documentId">Optional document ID to filter within a specific document</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>List of search results</returns>
        Task<List<VectorSearchResponse.SearchResult>> SearchVectorsAsync(
            float[] queryVector,
            int topK,
            Guid groupId,
            Guid? documentId = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Delete vector from collection
        /// </summary>
        /// <param name="id">ID of vector to delete</param>
        /// <returns>True if delete successful</returns>
        Task<bool> DeleteVectorAsync(string id);

        /// <summary>
        /// Search vectors across multiple groups (for studio-level AI)
        /// Uses Qdrant MatchAny filter on groupId
        /// </summary>
        /// <param name="queryVector">Query vector for search</param>
        /// <param name="topK">Number of results to return</param>
        /// <param name="groupIds">List of group IDs to search across</param>
        /// <param name="documentId">Optional document ID to filter within a specific document</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>List of search results from all specified groups</returns>
        Task<List<VectorSearchResponse.SearchResult>> SearchVectorsMultiGroupAsync(
            float[] queryVector,
            int topK,
            List<Guid> groupIds,
            Guid? documentId = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Test connection to vector database (for health checks)
        /// </summary>
        Task TestConnectionAsync();
    }
}
