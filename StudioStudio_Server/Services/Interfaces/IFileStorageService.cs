namespace StudioStudio_Server.Services.Interfaces
{
    /// <summary>
    /// Interface cho cloud file storage operations
    /// Implementations: BackblazeStorageService, AWS S3, Azure Blob (future)
    /// </summary>
    public interface IFileStorageService
    {
        /// <summary>
        /// T?o presigned URL cho upload file
        /// </summary>
        /// <param name="key">��?ng d?n file (v� d?: group/{groupId}/file.pdf)</param>
        /// <param name="expirationMinutes">Th?i gian h?t h?n (m?c �?nh: 10 ph�t)</param>
        /// <returns>Presigned URL cho HTTP PUT</returns>
        Task<string> GeneratePresignedUploadUrlAsync(string key, int expirationMinutes = 10);

        /// <summary>
        /// T?o presigned URL cho download file
        /// </summary>
        /// <param name="key">��?ng d?n file</param>
        /// <param name="expirationMinutes">Th?i gian h?t h?n (m?c �?nh: 60 ph�t)</param>
        /// <returns>Presigned URL cho HTTP GET</returns>
        Task<string> GeneratePresignedDownloadUrlAsync(string key, int expirationMinutes = 60);

        /// <summary>
        /// X�a file v?nh vi?n kh?i storage
        /// </summary>
        /// <param name="key">��?ng d?n file c?n x�a</param>
        /// <returns>True n?u x�a th�nh c�ng</returns>
        Task<bool> DeleteFileAsync(string key);

        /// <summary>
        /// Ki?m tra file c� t?n t?i kh�ng
        /// </summary>
        /// <param name="key">��?ng d?n file</param>
        /// <returns>True n?u file t?n t?i</returns>
        Task<bool> FileExistsAsync(string key);

        /// <summary>
        /// Download file t? storage
        /// </summary>
        /// <param name="key">��?ng d?n file</param>
        /// <returns>Stream c?a file</returns>
        Task<Stream> DownloadFileAsync(string key);

        /// <summary>
        /// Test connection to storage service (for health checks)
        /// </summary>
        Task TestConnectionAsync();
    }
}
