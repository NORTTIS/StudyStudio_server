
namespace StudioStudio_Server.Utils
{
    /// <summary>
    /// Utility class for working with HTTP context and localization
    /// </summary>
    public static class HttpContextHelper
    {
        /// <summary>
        /// Get culture code from Accept-Language header
        /// Default: "vi" (Vietnamese)
        /// Supported: "vi" (Vietnamese), "en" (English - for all other languages)
        /// 
        /// Logic:
        /// - If header is missing or empty ? "vi"
        /// - If language starts with "vi" ? "vi"
        /// - Otherwise ? "en" (English for all other languages)
        /// 
        /// Examples:
        /// - null ? "vi"
        /// - "vi-VN,vi;q=0.9" ? "vi"
        /// - "en-US,en;q=0.9" ? "en"
        /// - "ja-JP,ja;q=0.9" ? "en"
        /// - "fr-FR,fr;q=0.9" ? "en"
        /// </summary>
        /// <param name="context">HTTP context</param>
        /// <returns>Culture code ("vi" or "en")</returns>
        public static string GetCultureFromHeader(HttpContext context)
        {
            var lang = context.Request.Headers["Accept-Language"].FirstOrDefault();

            if (string.IsNullOrWhiteSpace(lang))
                return "vi";

            var culture = lang.Split(',')[0].Split('-')[0].Trim().ToLower();

            // Only Vietnamese returns "vi", everything else returns "en"
            return culture.StartsWith("vi") ? "vi" : "en";
        }
    }
}
