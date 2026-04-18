
namespace StudioStudio_Server.Utils
{
    /// <summary>
    /// Helper class for building absolute avatar URLs from relative paths
    /// </summary>
    public static class AvatarUrlHelper
    {
        /// <summary>
        /// Build absolute avatar URL from relative path
        /// Example: "/uploads/avatars/avatar.jpg" => "https://api.example.com/uploads/avatars/avatar.jpg"
        /// </summary>
        /// <param name="avatarUrl">Relative or absolute avatar URL</param>
        /// <param name="httpContext">HTTP context for building absolute URL</param>
        /// <returns>Absolute URL if input is relative path, otherwise returns original URL</returns>
        public static string? BuildAbsoluteAvatarUrl(string? avatarUrl, HttpContext? httpContext)
        {
            if (!string.IsNullOrEmpty(avatarUrl) && avatarUrl.StartsWith("/"))
            {
                var request = httpContext?.Request;
                if (request != null)
                {
                    return $"{request.Scheme}://{request.Host}{avatarUrl}";
                }
            }
            return avatarUrl;
        }
    }
}
