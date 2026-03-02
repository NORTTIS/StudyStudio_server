namespace StudioStudio_Server.Configurations
{
    /// <summary>
    /// C?u h?nh Qdrant Cloud vector database
    /// Binds: appsettings.json -> "Qdrant" section
    /// </summary>
    public class QdrantConfig
    {
        /// <summary>
        /// Qdrant Cloud endpoint URL
        /// Example: https://xyz-example.eu-central.aws.cloud.qdrant.io:6333
        /// </summary>
        public string Endpoint { get; set; } = string.Empty;

        /// <summary>
        /// API Key (t? Qdrant Cloud dashboard)
        /// </summary>
        public string ApiKey { get; set; } = string.Empty;

        /// <summary>
        /// Collection name ð? lýu vectors
        /// </summary>
        public string CollectionName { get; set; } = string.Empty;

        /// <summary>
        /// Vector dimension size (m?c ð?nh: 1536 cho OpenAI embeddings)
        /// </summary>
        public int VectorSize { get; set; } = 1536;

        /// <summary>
        /// Timeout cho requests (seconds, m?c ð?nh: 30)
        /// </summary>
        public int TimeoutSeconds { get; set; } = 30;
    }
}
