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
        /// Note: N?u config kh�ng �?y �?, service ho?t �?ng ? degraded mode (ch? log warning)
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
                _logger.LogWarning("Backblaze B2 storage ch�a ��?c c?u h?nh. C�c thao t�c l�u tr? file s? b? b? qua.");
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
        /// Flow: Backend t?o URL ? Frontend PUT file tr?c ti?p l�n B2
        /// </summary>
        /// <param name="key">File path (format: group/{groupId}/{docId}.pdf)</param>
        /// <param name="expirationMinutes">Th?i gian h?t h?n (m?c �?nh: 10 ph�t)</param>
        /// <returns>Presigned URL ho?c empty string n?u ch�a config</returns>
        public async Task<string> GeneratePresignedUploadUrlAsync(string key, int expirationMinutes = 10)
        {
            return await GeneratePresignedUploadUrlAsync(key, _config.BucketName, expirationMinutes);
        }

        /// <summary>
        /// T?o presigned URL cho upload file v?i bucket t�y ch?nh
        /// </summary>
        public async Task<string> GeneratePresignedUploadUrlAsync(string key, string bucketName, int expirationMinutes = 10)
        {
            if (_s3Client == null)
            {
                _logger.LogWarning("Backblaze B2 storage ch�a ��?c c?u h?nh. Kh�ng th? t?o presigned upload URL cho key: {Key}", key);
                return string.Empty;
            }

            GetPreSignedUrlRequest request = new GetPreSignedUrlRequest
            {
                BucketName = bucketName,
                Key = key,
                Verb = HttpVerb.PUT,
                Expires = DateTime.UtcNow.AddMinutes(expirationMinutes)
            };

            string url = _s3Client.GetPreSignedURL(request);
            _logger.LogInformation("�? t?o presigned upload URL cho key: {Key}, bucket: {Bucket}", key, bucketName);

            return url;
        }

        /// <summary>
        /// T?o presigned URL cho download file
        /// Flow: Backend t?o URL ? Frontend GET file tr?c ti?p t? B2
        /// </summary>
        /// <param name="key">File path</param>
        /// <param name="expirationMinutes">Th?i gian h?t h?n (m?c �?nh: 60 ph�t)</param>
        /// <returns>Presigned URL ho?c empty string n?u ch�a config</returns>
        public async Task<string> GeneratePresignedDownloadUrlAsync(string key, int expirationMinutes = 60)
        {
            if (_s3Client == null)
            {
                _logger.LogWarning("Backblaze B2 storage ch�a ��?c c?u h?nh. Kh�ng th? t?o presigned download URL cho key: {Key}", key);
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
            _logger.LogInformation("�? t?o presigned download URL cho key: {Key}", key);

            return url;
        }

        /// <summary>
        /// X�a file v?nh vi?n kh?i Backblaze B2
        /// Note: Lu�n ki?m tra quy?n tr�?c khi g?i. X�a kh�ng th? ho�n t�c.
        /// </summary>
        /// <param name="key">File path c?n x�a</param>
        /// <returns>True n?u th�nh c�ng, false n?u th?t b?i</returns>
        public async Task<bool> DeleteFileAsync(string key)
        {
            return await DeleteFileAsync(key, _config.BucketName);
        }

        /// <summary>
        /// X�a file v?nh vi?n kh?i Backblaze B2 v?i bucket t�y ch?nh
        /// </summary>
        public async Task<bool> DeleteFileAsync(string key, string bucketName)
        {
            if (_s3Client == null)
            {
                _logger.LogWarning("Backblaze B2 storage ch�a ��?c c?u h?nh. Kh�ng th? x�a file v?i key: {Key}", key);
                return false;
            }

            DeleteObjectRequest request = new DeleteObjectRequest
            {
                BucketName = bucketName,
                Key = key
            };

            DeleteObjectResponse response = await _s3Client.DeleteObjectAsync(request);
            _logger.LogInformation("�? x�a file v?i key: {Key}, bucket: {Bucket}", key, bucketName);

            return response.HttpStatusCode == System.Net.HttpStatusCode.NoContent;
        }

        /// <summary>
        /// Ki?m tra file c� t?n t?i trong B2 kh�ng
        /// </summary>
        /// <param name="key">File path c?n ki?m tra</param>
        /// <returns>True n?u file t?n t?i</returns>
        public async Task<bool> FileExistsAsync(string key)
        {
            return await FileExistsAsync(key, _config.BucketName);
        }

        /// <summary>
        /// Ki?m tra file c� t?n t?i trong B2 v?i bucket t�y ch?nh
        /// </summary>
        public async Task<bool> FileExistsAsync(string key, string bucketName)
        {
            if (_s3Client == null)
            {
                _logger.LogWarning("Backblaze B2 storage ch�a ��?c c?u h?nh. Kh�ng th? ki?m tra file v?i key: {Key}", key);
                return false;
            }

            try
            {
                GetObjectMetadataRequest request = new GetObjectMetadataRequest
                {
                    BucketName = bucketName,
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
        /// Tr? v? Stream �? �?c file content
        /// </summary>
        /// <param name="key">File path c?n download</param>
        /// <returns>Stream c?a file</returns>
        public async Task<Stream> DownloadFileAsync(string key)
        {
            if (_s3Client == null)
            {
                _logger.LogWarning("Backblaze B2 storage ch�a ��?c c?u h?nh. Kh�ng th? download file v?i key: {Key}", key);
                throw new Exception("Backblaze B2 storage ch�a ��?c c?u h?nh");
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
                throw new Exception($"Kh�ng th? download file: {ex.Message}", ex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "L?i kh�ng x�c �?nh khi download file. Key: {Key}", key);
                throw;
            }
        }

        /// <summary>
        /// Test connection to Backblaze B2 storage
        /// </summary>
        public async Task TestConnectionAsync()
        {
            if (_s3Client == null)
            {
                throw new Exception("Backblaze B2 storage ch�a ��?c c?u h?nh");
            }

            // Test by listing buckets or checking bucket access
            var request = new ListBucketsRequest();
            var response = await _s3Client.ListBucketsAsync(request);

            if (response.Buckets == null)
            {
                throw new Exception("Failed to connect to Backblaze B2");
            }
        }
    }
}
