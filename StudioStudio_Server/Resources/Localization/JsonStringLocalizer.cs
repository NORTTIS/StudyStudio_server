using System.Text.Json;

namespace StudioStudio_Server.Localization
{
    public class JsonStringLocalizer
    {
        private readonly IDictionary<string, string> _messages;

        public JsonStringLocalizer(IWebHostEnvironment env, string culture)
        {
            var path = Path.Combine(
                env.ContentRootPath,
                "Resources",
                "Errors",
                $"errors.{culture}.json");

            if (!File.Exists(path))
            {
                _messages = new Dictionary<string, string>();
                return;
            }

            var json = File.ReadAllText(path);
            _messages = JsonSerializer.Deserialize<Dictionary<string, string>>(json)
                        ?? new Dictionary<string, string>();
        }

        public string Get(string key)
        {
            return _messages.TryGetValue(key, out var value)
                ? value
                : key; // fallback: return the key itself if not found
        }
    }
}
