namespace StudioStudio_Server.Configurations
{
    /// <summary>
    /// Cấu hình Backblaze B2 cloud storage
    /// Binds: appsettings.json -> "Backblaze" section
    /// </summary>
    public class BackblazeConfig
    {
        /// <summary>
        /// S3-compatible endpoint URL
        /// Example: https://s3.us-west-004.backblazeb2.com
        /// </summary>
        public string ServiceUrl { get; set; } = string.Empty;

        /// <summary>
        /// Application Key ID (từ Backblaze B2 App Keys)
        /// </summary>
        public string KeyId { get; set; } = string.Empty;

        /// <summary>
        /// Application Key (giữ bí mật như password)
        /// </summary>
        public string AppKey { get; set; } = string.Empty;

        /// <summary>
        /// Tên bucket để lưu trữ file
        /// </summary>
        public string BucketName { get; set; } = string.Empty;

        /// <summary>
        /// Region identifier (ví dụ: us-west-004, eu-central-003)
        /// </summary>
        public string Region { get; set; } = string.Empty;

        /// <summary>
        /// Bucket for public files (avatars, etc.)
        /// </summary>
        public string PublicBucketName { get; set; } = "studystudio-public";
    }
}
