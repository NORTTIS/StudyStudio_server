namespace StudioStudio_Server.Configurations
{
    /// <summary>
    /// Qdrant Cloud vector database configuration
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
        /// API Key (from Qdrant Cloud dashboard)
        /// </summary>
        public string ApiKey { get; set; } = string.Empty;

        /// <summary>
        /// Collection name to store vectors
        /// </summary>
        public string CollectionName { get; set; } = string.Empty;

        /// <summary>
        /// Vector dimension size
        /// </summary>
        public int VectorSize { get; set; } = 758;

        /// <summary>
        /// Timeout for requests (seconds, default: 30)
        /// Increased from 20 to 30 to handle slower delete operations
        /// </summary>
        public int TimeoutSeconds { get; set; } = 30;
    }
}
