using StudioStudio_Server.Models.Enums;
using StudioStudio_Server.Resources.Localization;

namespace StudioStudio_Server.Services
{
    public interface IMessageService
    {
        string GetMessage(string code);
        string GetMessage(string code, SupportedLanguage language);
    }

    /// <summary>
    /// Service handling localized message retrieval
    /// Loads messages from JSON localization files based on language
    /// Supported languages: Vietnamese (default), English
    /// Language detection: Uses Accept-Language header from HTTP request
    /// </summary>
    public class MessageService(IWebHostEnvironment env, IHttpContextAccessor httpContextAccessor) : IMessageService
    {
        /// <summary>
        /// Get localized message by code
        /// Language is automatically detected from Accept-Language header
        /// </summary>
        public string GetMessage(string code)
        {
            var language = GetCurrentLanguage();
            return GetMessage(code, language);
        }

        /// <summary>
        /// Get localized message by code with specific language
        /// Loads message from JSON file in Resources/Localization folder
        /// </summary>
        public string GetMessage(string code, SupportedLanguage language)
        {
            var cultureCode = language.ToCultureCode();
            var localizer = new JsonStringLocalizer(env, cultureCode);
            return localizer.Get(code);
        }

        /// <summary>
        /// Get current language from HTTP request header
        /// Uses Accept-Language header
        /// Default: Vietnamese if header not present or invalid
        /// </summary>
        public SupportedLanguage GetCurrentLanguage()
        {
            var context = httpContextAccessor.HttpContext;
            if (context == null)
            {
                return SupportedLanguage.Vietnamese; // Default
            }

            var acceptLanguage = context.Request.Headers["Accept-Language"].FirstOrDefault();
            return SupportedLanguageExtensions.FromCultureCode(acceptLanguage);
        }
    }
}
