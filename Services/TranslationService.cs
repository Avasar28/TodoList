using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;

namespace TodoListApp.Services
{
    public class TranslationService : ITranslationService
    {
        private readonly HttpClient _httpClient;
        private readonly IMemoryCache _cache;
        private const string BaseUrl = "https://api.mymemory.translated.net/get";

        public TranslationService(HttpClient httpClient, IMemoryCache cache)
        {
            _httpClient = httpClient;
            _cache = cache;
        }

        public async Task<string> TranslateAsync(string text, string targetLang)
        {
            if (string.IsNullOrWhiteSpace(text)) return text;
            if (targetLang.ToLower() == "en") return text; // Assuming source is EN

            string cacheKey = $"Trans_{text.GetHashCode()}_{targetLang}";
            if (_cache.TryGetValue(cacheKey, out string cachedTranslation))
            {
                return cachedTranslation;
            }

            try
            {
                // MyMemory API requires source|target pair
                var url = $"{BaseUrl}?q={Uri.EscapeDataString(text)}&langpair=en|{targetLang}&de=avsar@example.com";
                
                var response = await _httpClient.GetStringAsync(url);
                using var doc = JsonDocument.Parse(response);
                var translatedText = doc.RootElement.GetProperty("responseData").GetProperty("translatedText").GetString();

                // Advanced validation: If response status != 200, MyMemory might return error or untranslated text
                var status = doc.RootElement.GetProperty("responseStatus").GetInt32();
                
                if (status == 200 && !string.IsNullOrEmpty(translatedText))
                {
                    // Cache for 24 hours
                    _cache.Set(cacheKey, translatedText, TimeSpan.FromHours(24));
                    return translatedText;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Translation error: {ex.Message}");
            }

            return text; // Fallback to original
        }

        public async Task<string[]> TranslateBatchAsync(string[] texts, string targetLang)
        {
            // MyMemory doesn't support batch nicely in free tier, so we parallelize
            // In a real paid scenario, we'd use a bulk endpoint
            
            var tasks = texts.Select(t => TranslateAsync(t, targetLang));
            return await Task.WhenAll(tasks);
        }
    }
}
