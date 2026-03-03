using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;
using StudioStudio_Server.Configurations;
using StudioStudio_Server.Services.Interfaces;

namespace StudioStudio_Server.Services
{
    /// <summary>
    /// Service x? l? file storage s? d?ng Backblaze B2 (S3-compatible)
    /// Pattern: Presigned URLs cho direct upload/download t? client
    /// </summary>
    public class BackblazeStorageService : IFileStorageService
    {
        private readonly BackblazeConfig _config;
        private readonly ILogger<BackblazeStorageService> _logger;
        private readonly AmazonS3Client _s3Client;

        /// <summary>
        /// Kh?i t?o Backblaze Storage Service
        /// Note: N?u config không ð?y ð?, service ho?t ð?ng ? degraded mode (ch? log warning)
        /// </summary>
        public BackblazeStorageService(
            IOptions<BackblazeConfig> config,
            ILogger<BackblazeStorageService> logger)
        {
            _config = config.Value;
            _logger = logger;

            if (string.IsNullOrEmpty(_config.ServiceUrl) ||
                string.IsNullOrEmpty(_config.KeyId) ||
                string.IsNullOrEmpty(_config.AppKey))
            {
                _logger.LogWarning("Backblaze B2 storage chýa ðý?c c?u h?nh. Các thao tác lýu tr? file s? b? b? qua.");
                _s3Client = null!;
                return;
            }

            AmazonS3Config s3Config = new AmazonS3Config
            {
                ServiceURL = _config.ServiceUrl,
                ForcePathStyle = true
            };

            _s3Client = new AmazonS3Client(_config.KeyId, _config.AppKey, s3Config);
        }

        /// <summary>
        /// T?o presigned URL cho upload file
        /// Flow: Backend t?o URL ? Frontend PUT file tr?c ti?p lên B2
        /// </summary>
        /// <param name="key">File path (format: group/{groupId}/{docId}.pdf)</param>
        /// <param name="expirationMinutes">Th?i gian h?t h?n (m?c ð?nh: 10 phút)</param>
        /// <returns>Presigned URL ho?c empty string n?u chýa config</returns>
        public async Task<string> GeneratePresignedUploadUrlAsync(string key, int expirationMinutes = 10)
        {
            if (_s3Client == null)
            {
                _logger.LogWarning("Backblaze B2 storage chýa ðý?c c?u h?nh. Không th? t?o presigned upload URL cho key: {Key}", key);
                return string.Empty;
            }

            GetPreSignedUrlRequest request = new GetPreSignedUrlRequest
            {
                BucketName = _config.BucketName,
                Key = key,
                Verb = HttpVerb.PUT,
                Expires = DateTime.UtcNow.AddMinutes(expirationMinutes)
            };

            string url = _s3Client.GetPreSignedURL(request);
            _logger.LogInformation("Ð? t?o presigned upload URL cho key: {Key}", key);

            return url;
        }

        /// <summary>
        /// T?o presigned URL cho download file
        /// Flow: Backend t?o URL ? Frontend GET file tr?c ti?p t? B2
        /// </summary>
        /// <param name="key">File path</param>
        /// <param name="expirationMinutes">Th?i gian h?t h?n (m?c ð?nh: 60 phút)</param>
        /// <returns>Presigned URL ho?c empty string n?u chýa config</returns>
        public async Task<string> GeneratePresignedDownloadUrlAsync(string key, int expirationMinutes = 60)
        {
            if (_s3Client == null)
            {
                _logger.LogWarning("Backblaze B2 storage chýa ðý?c c?u h?nh. Không th? t?o presigned download URL cho key: {Key}", key);
                return string.Empty;
            }

            GetPreSignedUrlRequest request = new GetPreSignedUrlRequest
            {
                BucketName = _config.BucketName,
                Key = key,
                Verb = HttpVerb.GET,
                Expires = DateTime.UtcNow.AddMinutes(expirationMinutes)
            };

            string url = _s3Client.GetPreSignedURL(request);
            _logger.LogInformation("Ð? t?o presigned download URL cho key: {Key}", key);

            return url;
        }

        /// <summary>
        /// Xóa file v?nh vi?n kh?i Backblaze B2
        /// Note: Luôn ki?m tra quy?n trý?c khi g?i. Xóa không th? hoàn tác.
        /// </summary>
        /// <param name="key">File path c?n xóa</param>
        /// <returns>True n?u thành công, false n?u th?t b?i</returns>
        public async Task<bool> DeleteFileAsync(string key)
        {
            if (_s3Client == null)
            {
                _logger.LogWarning("Backblaze B2 storage chýa ðý?c c?u h?nh. Không th? xóa file v?i key: {Key}", key);
                return false;
            }

            DeleteObjectRequest request = new DeleteObjectRequest
            {
                BucketName = _config.BucketName,
                Key = key
            };

            DeleteObjectResponse response = await _s3Client.DeleteObjectAsync(request);
            _logger.LogInformation("Ð? xóa file v?i key: {Key}", key);

            return response.HttpStatusCode == System.Net.HttpStatusCode.NoContent;
        }

        /// <summary>
        /// Ki?m tra file có t?n t?i trong B2 không
        /// </summary>
        /// <param name="key">File path c?n ki?m tra</param>
        /// <returns>True n?u file t?n t?i</returns>
        public async Task<bool> FileExistsAsync(string key)
        {
            if (_s3Client == null)
            {
                _logger.LogWarning("Backblaze B2 storage chýa ðý?c c?u h?nh. Không th? ki?m tra file v?i key: {Key}", key);
                return false;
            }

            try
            {
                GetObjectMetadataRequest request = new GetObjectMetadataRequest
                {
                    BucketName = _config.BucketName,
                    Key = key
                };

                await _s3Client.GetObjectMetadataAsync(request);
                return true;
            }
            catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "L?i khi ki?m tra file existence. Key: {Key}", key);
                return false;
            }
        }

        /// <summary>
        /// Download file t? B2 Storage
        /// Tr? v? Stream ð? ð?c file content
        /// </summary>
        /// <param name="key">File path c?n download</param>
        /// <returns>Stream c?a file</returns>
        public async Task<Stream> DownloadFileAsync(string key)
        {
            if (_s3Client == null)
            {
                _logger.LogWarning("Backblaze B2 storage chýa ðý?c c?u h?nh. Không th? download file v?i key: {Key}", key);
                throw new Exception("Backblaze B2 storage chýa ðý?c c?u h?nh");
            }

            try
            {
                GetObjectRequest request = new GetObjectRequest
                {
                    BucketName = _config.BucketName,
                    Key = key
                };

                GetObjectResponse response = await _s3Client.GetObjectAsync(request);
                _logger.LogInformation("Downloaded file from B2. Key: {Key}, Size: {Size} bytes", 
                    key, response.ContentLength);

                return response.ResponseStream;
            }
            catch (AmazonS3Exception ex)
            {
                _logger.LogError(ex, "L?i khi download file t? B2. Key: {Key}, Status: {Status}", 
                    key, ex.StatusCode);
                throw new Exception($"Không th? download file: {ex.Message}", ex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "L?i không xác ð?nh khi download file. Key: {Key}", key);
                throw;
            }
        }
    }
}
