using System.Collections.Concurrent;
using System.Text.Json;

namespace StudioStudio_Server.Resources.Localization
{
    public class JsonStringLocalizer
    {
        private static readonly ConcurrentDictionary<string, IDictionary<string, string>> _cache = new();
        private readonly IDictionary<string, string> _messages;

        public JsonStringLocalizer(IWebHostEnvironment env, string culture)
        {
            var cacheKey = $"errors.{culture}";

            if (!_cache.TryGetValue(cacheKey, out var cachedMessages))
            {
                var path = Path.Combine(
                    env.ContentRootPath,
                    "Resources",
                    "Errors",
                    $"errors.{culture}.json");

                if (!File.Exists(path))
                {
                    cachedMessages = new Dictionary<string, string>();
                }
                else
                {
                    var json = File.ReadAllText(path);
                    cachedMessages = JsonSerializer.Deserialize<Dictionary<string, string>>(json)
                                ?? new Dictionary<string, string>();
                }

                _cache.TryAdd(cacheKey, cachedMessages);
            }

            _messages = cachedMessages;
        }

        public string Get(string key)
        {
            return _messages.TryGetValue(key, out var value)
                ? value
                : key;
        }
    }
}
