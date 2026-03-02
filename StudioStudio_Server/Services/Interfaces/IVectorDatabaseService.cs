using StudioStudio_Server.Models.DTOs.Response;

namespace StudioStudio_Server.Services.Interfaces
{
    /// <summary>
    /// Interface cho vector database operations (Qdrant)
    /// Implementations: QdrantService
    /// </summary>
    public interface IVectorDatabaseService
    {
        /// <summary>
        /// Thêm vector vào collection
        /// </summary>
        /// <param name="id">Unique ID cho vector</param>
        /// <param name="vector">Vector embeddings (float array)</param>
        /// <param name="payload">Metadata (JSON object)</param>
        /// <returns>True n?u thành công</returns>
        Task<bool> UpsertVectorAsync(string id, float[] vector, Dictionary<string, object> payload);

        /// <summary>
        /// T?m ki?m vectors týõng t?
        /// </summary>
        /// <param name="queryVector">Query vector ð? search</param>
        /// <param name="limit">S? lý?ng k?t qu? (m?c ð?nh: 5)</param>
        /// <param name="filters">Filters cho payload (optional)</param>
        /// <returns>Danh sách vectors týõng t? kèm score</returns>
        Task<List<VectorSearchResult>> SearchSimilarAsync(float[] queryVector, int limit = 5, Dictionary<string, object>? filters = null);

        /// <summary>
        /// T?m ki?m vectors v?i filter groupId (cho AI Q&A)
        /// </summary>
        /// <param name="queryVector">Query vector ð? search</param>
        /// <param name="topK">S? lý?ng k?t qu? tr? v?</param>
        /// <param name="groupId">Group ID ð? filter</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Danh sách search results</returns>
        Task<List<VectorSearchResponse.SearchResult>> SearchVectorsAsync(
            float[] queryVector,
            int topK,
            Guid groupId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Xóa vector kh?i collection
        /// </summary>
        /// <param name="id">ID c?a vector c?n xóa</param>
        /// <returns>True n?u xóa thành công</returns>
        Task<bool> DeleteVectorAsync(string id);

        /// <summary>
        /// Xóa nhi?u vectors theo filter
        /// </summary>
        /// <param name="filters">Filters ð? xác ð?nh vectors c?n xóa</param>
        /// <returns>True n?u xóa thành công</returns>
        Task<bool> DeleteVectorsByFilterAsync(Dictionary<string, object> filters);

        /// <summary>
        /// L?y thông tin vector theo ID
        /// </summary>
        /// <param name="id">ID c?a vector</param>
        /// <returns>Vector data ho?c null n?u không t?m th?y</returns>
        Task<VectorSearchResult?> GetVectorByIdAsync(string id);
    }

    /// <summary>
    /// K?t qu? t?m ki?m vector
    /// </summary>
    public class VectorSearchResult
    {
        public string Id { get; set; } = string.Empty;
        public float Score { get; set; }
        public Dictionary<string, object> Payload { get; set; } = new();
        public float[]? Vector { get; set; }
    }
}
