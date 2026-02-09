using StudioStudio_Server.Localization;

namespace StudioStudio_Server.Services.Interfaces
{
    public interface IMessageService
    {
        string GetMessage(string code);
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
            var culture = GetCulture();
            var localizer = new JsonStringLocalizer(_env, culture);
            return localizer.Get(code);
        }

        private string GetCulture()
        {
            var context = _httpContextAccessor.HttpContext;
            if (context == null) return "vi";

            var lang = context.Request.Headers["Accept-Language"].FirstOrDefault();
            return string.IsNullOrWhiteSpace(lang) ? "vi" : lang.Split(',')[0];
        }
    }
}
