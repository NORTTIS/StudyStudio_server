namespace StudioStudio_Server.Models.Enums
{
    /// <summary>
    /// Supported languages in the system
    /// Only Vietnamese and English are supported
    /// </summary>
    public enum SupportedLanguage
    {
        Vietnamese,
        English
    }
    public static class SupportedLanguageExtensions
    {
        /// <summary>
        /// Convert SupportedLanguage enum to culture code string
        /// </summary>
        /// <param name="language">The language enum value</param>
        /// <returns>Culture code (vi or en)</returns>
        public static string ToCultureCode(this SupportedLanguage language)
        {
            return language switch
            {
                SupportedLanguage.Vietnamese => "vi",
                SupportedLanguage.English => "en",
                _ => "vi" // Default to Vietnamese
            };
        }

        /// <summary>
        /// Parse culture code string to SupportedLanguage enum
        /// Supports full culture codes (vi-VN, en-US) and short codes (vi, en)
        /// </summary>
        /// <param name="cultureCode">Culture code string (e.g., "vi", "en", "vi-VN", "en-US")</param>
        /// <returns>SupportedLanguage enum value</returns>
        public static SupportedLanguage FromCultureCode(string? cultureCode)
        {
            if (string.IsNullOrWhiteSpace(cultureCode))
            {
                return SupportedLanguage.Vietnamese; // Default
            }

            // Extract language code (first part before hyphen or comma)
            string languageCode = cultureCode.Split('-', ',')[0].Trim().ToLowerInvariant();

            return languageCode switch
            {
                "en" => SupportedLanguage.English,
                "vi" => SupportedLanguage.Vietnamese,
                _ => SupportedLanguage.Vietnamese
            };
        }

    }
}
