using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
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
            // Add User-Agent to look like a browser/valid client prevents some blocking
            if (!_httpClient.DefaultRequestHeaders.Contains("User-Agent"))
            {
                _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36");
            }
        }

        public async Task<string> TranslateAsync(string text, string targetLang)
        {
            if (string.IsNullOrWhiteSpace(text)) return text;
            if (targetLang.ToLower() == "en") return text;

            string cacheKey = $"Trans_{text.GetHashCode()}_{targetLang}";
            if (_cache.TryGetValue(cacheKey, out string cachedTranslation))
            {
                return cachedTranslation;
            }

            int retries = 0;
            const int maxRetries = 3;

            while (retries <= maxRetries)
            {
                try
                {
                    var url = $"{BaseUrl}?q={Uri.EscapeDataString(text)}&langpair=en|{targetLang}&de=avsar@example.com";
                    
                    // Use GetAsync to inspect status code before it throws
                    var response = await _httpClient.GetAsync(url);

                    if (response.StatusCode == HttpStatusCode.TooManyRequests)
                    {
                        retries++;
                        if (retries > maxRetries) break;

                        // Exponential backoff: 2s, 4s, 8s...
                        int delay = 2000 * (int)Math.Pow(2, retries - 1);
                        // Add jitter
                        delay += Random.Shared.Next(0, 500);
                        
                        Console.WriteLine($"[Translation] 429 Rate Limit. Retrying in {delay}ms...");
                        await Task.Delay(delay);
                        continue;
                    }

                    response.EnsureSuccessStatusCode();

                    var json = await response.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(json);
                    
                    // MyMemory sometimes returns 200 OK but with an error status inside JSON
                    var internalStatus = doc.RootElement.GetProperty("responseStatus").GetInt32();
                    if (internalStatus == 429)
                    {
                        // Handle internal 429 same as HTTP 429
                         retries++;
                        if (retries > maxRetries) break;
                        
                        int delay = 2000 * (int)Math.Pow(2, retries - 1);
                        await Task.Delay(delay);
                        continue;
                    }

                    var translatedText = doc.RootElement.GetProperty("responseData").GetProperty("translatedText").GetString();

                    if (internalStatus == 200 && !string.IsNullOrEmpty(translatedText))
                    {
                        _cache.Set(cacheKey, translatedText, TimeSpan.FromHours(24));
                        return translatedText;
                    }

                    // If we get here, it's a non-retriable error (e.g. invalid query)
                    break; 
                }
                catch (Exception ex)
                {
                   Console.WriteLine($"[Translation] Error Try {retries}: {ex.Message}");
                   
                   // Retry on generic network/http errors too
                   retries++;
                   if (retries <= maxRetries)
                   {
                        int delay = 2000 * (int)Math.Pow(2, retries - 1);
                        await Task.Delay(delay);
                        continue;
                   }
                   break;
                }
            }

            return text; // Fallback
        }

        public async Task<string[]> TranslateBatchAsync(string[] texts, string targetLang)
        {
            // Use Semaphore to allow controlled concurrency (faster than strict sequential, safer than fully parallel)
            // Limit to 3 concurrent requests to respect API rate limits while improving speed.
            
            var results = new string[texts.Length];
            using var semaphore = new SemaphoreSlim(3); 
            
            var tasks = texts.Select(async (text, index) =>
            {
                await semaphore.WaitAsync();
                try
                {
                    // Minimal delay to prevent bursting too hard
                    await Task.Delay(Random.Shared.Next(50, 150)); 
                    results[index] = await TranslateAsync(text, targetLang);
                }
                finally
                {
                    semaphore.Release();
                }
            });

            await Task.WhenAll(tasks);
            return results;
        }
    }
}
