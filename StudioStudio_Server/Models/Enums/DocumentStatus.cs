namespace StudioStudio_Server.Models.Enums
{
    /// <summary>
    /// Tr?ng thái c?a document trong quá tr?nh upload và processing
    /// </summary>
    public enum DocumentStatus
    {
        /// <summary>
        /// Presigned URL ð? ðý?c t?o, ðang ch? frontend upload
        /// </summary>
        Uploading = 0,

        /// <summary>
        /// File ð? upload xong, ðang x? l? embedding
        /// </summary>
        Processing = 1,

        /// <summary>
        /// Ð? hoàn t?t embedding và lýu vào Qdrant
        /// </summary>
        Completed = 2,

        /// <summary>
        /// X? l? th?t b?i
        /// </summary>
        Failed = 3
    }
}
