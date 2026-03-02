namespace StudioStudio_Server.Models.DTOs.Response
{
    /// <summary>
    /// Response cho vector search t? Qdrant
    /// S? d?ng cho AI Q&A service
    /// </summary>
    public class VectorSearchResponse
    {
        public List<SearchResult> Results { get; set; } = new();
        public int TotalResults { get; set; }

        /// <summary>
        /// K?t qu? t? Qdrant search
        /// </summary>
        public class SearchResult
        {
            public string Id { get; set; } = string.Empty;
            public float Score { get; set; }
            public Dictionary<string, object> Payload { get; set; } = new();
        }
    }
}
