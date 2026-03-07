using StudioStudio_Server.Localization;
using StudioStudio_Server.Models.Enums;

namespace StudioStudio_Server.Services.Interfaces
{
    public interface IMessageService
    {
        string GetMessage(string code);
        string GetMessage(string code, SupportedLanguage language);
        SupportedLanguage GetCurrentLanguage();
    }

    /// <summary>
    /// Service handling localized message retrieval
    /// Loads messages from JSON localization files based on language
    /// Supported languages: Vietnamese (default), English
    /// Language detection: Uses Accept-Language header from HTTP request
    /// </summary>
    public class MessageService : IMessageService
    {
        private readonly IWebHostEnvironment _env;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public MessageService(IWebHostEnvironment env, IHttpContextAccessor httpContextAccessor)
        {
            _env = env;
            _httpContextAccessor = httpContextAccessor;
        }

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
            var localizer = new JsonStringLocalizer(_env, cultureCode);
            return localizer.Get(code);
        }

        /// <summary>
        /// Get current language from HTTP request header
        /// Uses Accept-Language header
        /// Default: Vietnamese if header not present or invalid
        /// </summary>
        public SupportedLanguage GetCurrentLanguage()
        {
            var context = _httpContextAccessor.HttpContext;
            if (context == null)
            {
                return SupportedLanguage.Vietnamese; // Default
            }

            var acceptLanguage = context.Request.Headers["Accept-Language"].FirstOrDefault();
            return SupportedLanguageExtensions.FromCultureCode(acceptLanguage);
        }

        /// <summary>
        /// Get culture code from current language
        /// Helper method for localization
        /// </summary>
        private string GetCulture()
        {
            var language = GetCurrentLanguage();
            return language.ToCultureCode();
        }
    }
}
