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

    public class MessageService : IMessageService
    {
        private readonly IWebHostEnvironment _env;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public MessageService(IWebHostEnvironment env, IHttpContextAccessor httpContextAccessor)
        {
            _env = env;
            _httpContextAccessor = httpContextAccessor;
        }

        public string GetMessage(string code)
        {
            var language = GetCurrentLanguage();
            return GetMessage(code, language);
        }

        public string GetMessage(string code, SupportedLanguage language)
        {
            var cultureCode = language.ToCultureCode();
            var localizer = new JsonStringLocalizer(_env, cultureCode);
            return localizer.Get(code);
        }

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

        private string GetCulture()
        {
            var language = GetCurrentLanguage();
            return language.ToCultureCode();
        }
    }
}
