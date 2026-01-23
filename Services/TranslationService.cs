using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace TodoListApp.Services
{
    public class TranslationService : ITranslationService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly ILogger<TranslationService> _logger;

        // Public Mirrors for high availability
        private readonly string[] _apiMirrors = new[] 
        {
            "https://translate.fedilab.app/translate"
        };

        // In-Memory Cache
        private static readonly ConcurrentDictionary<string, string> _memoryCache = new();

        public TranslationService(HttpClient httpClient, IConfiguration configuration, ILogger<TranslationService> logger)
        {
            _httpClient = httpClient;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<string[]> TranslateBatchAsync(string[] texts, string targetLang)
        {
            if (texts == null || texts.Length == 0) return Array.Empty<string>();
            if (string.IsNullOrEmpty(targetLang) || targetLang.ToLower() == "en") return texts;

            var results = new string[texts.Length];
            var missingIndices = new List<int>();
            var missingTexts = new List<string>();

            // 1. Check Cache
            for (int i = 0; i < texts.Length; i++)
            {
                var text = texts[i];
                if (string.IsNullOrWhiteSpace(text))
                {
                    results[i] = text;
                    continue;
                }

                string key = $"{targetLang}:{text}";
                if (_memoryCache.TryGetValue(key, out var cached))
                {
                    results[i] = cached;
                }
                else
                {
                    missingIndices.Add(i);
                    missingTexts.Add(text);
                }
            }

            if (missingTexts.Count == 0) return results;

            // 2. Prepare API Request
            // LibreTranslate Batch: q acts as array
            var requestObj = new
            {
                q = missingTexts.ToArray(),
                source = "en",
                target = targetLang,
                format = "text"
            };

            var json = JsonSerializer.Serialize(requestObj);
            bool success = false;
            
            // 3. Try Mirrors
            var urlsToTry = new List<string>();
            var configUrl = _configuration["Translation:ApiUrl"];
            if (!string.IsNullOrEmpty(configUrl)) urlsToTry.Add(configUrl);
            urlsToTry.AddRange(_apiMirrors);

            foreach (var url in urlsToTry.Distinct())
            {
                try
                {
                    var content = new StringContent(json, Encoding.UTF8, "application/json");
                    
                    // Short timeout for fallback speed
                    using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(10));
                    
                    var response = await _httpClient.PostAsync(url, content, cts.Token);
                    
                    if (!response.IsSuccessStatusCode) continue;

                    var responseJson = await response.Content.ReadAsStringAsync(cts.Token);
                    using var doc = JsonDocument.Parse(responseJson);

                    // Parse: { "translatedText": [ ... ] } or { "translatedText": "..." }
                    if (doc.RootElement.TryGetProperty("translatedText", out var translatedEl))
                    {
                         if (translatedEl.ValueKind == JsonValueKind.Array)
                         {
                             var translations = translatedEl.EnumerateArray().Select(x => x.GetString()).ToArray();
                             for (int i = 0; i < translations.Length; i++)
                             {
                                 if (i >= missingIndices.Count) break;
                                 var val = translations[i];
                                 results[missingIndices[i]] = val;
                                 if (!string.IsNullOrEmpty(val)) _memoryCache.TryAdd($"{targetLang}:{missingTexts[i]}", val);
                             }
                             success = true;
                             break;
                         }
                         else if (translatedEl.ValueKind == JsonValueKind.String && missingIndices.Count == 1)
                         {
                             var val = translatedEl.GetString();
                             results[missingIndices[0]] = val;
                             if (!string.IsNullOrEmpty(val)) _memoryCache.TryAdd($"{targetLang}:{missingTexts[0]}", val);
                             success = true;
                             break;
                         }
                    }
                    // Some versions return array directly [ "..." ]
                    else if (doc.RootElement.ValueKind == JsonValueKind.Array)
                    {
                         var translations = doc.RootElement.EnumerateArray().Select(x => x.GetString()).ToArray();
                         for (int i = 0; i < translations.Length; i++)
                         {
                             if (i >= missingIndices.Count) break;
                             var val = translations[i];
                             results[missingIndices[i]] = val;
                             if (!string.IsNullOrEmpty(val)) _memoryCache.TryAdd($"{targetLang}:{missingTexts[i]}", val);
                         }
                         success = true;
                         break;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"Mirror {url} failed: {ex.Message}");
                }
            }

            // 4. Fallback if all failed: Try Google Translate (GTX - Free Endpoint)
            if (!success)
            {
                _logger.LogWarning("LibreTranslate mirrors failed. Attempting Google GTX fallback...");
                
                var tasks = missingIndices.Select(async idx => 
                {
                    try 
                    {
                        var text = texts[idx];
                        var url = $"https://translate.googleapis.com/translate_a/single?client=gtx&sl=auto&tl={targetLang}&dt=t&q={Uri.EscapeDataString(text)}";
                        
                        var response = await _httpClient.GetAsync(url);
                        if (response.IsSuccessStatusCode)
                        {
                            var jsonStr = await response.Content.ReadAsStringAsync();
                            using var doc = JsonDocument.Parse(jsonStr);
                            // Response is [[[ "Translated", "Original", ... ], [ "Translated2", ... ]]]
                            if (doc.RootElement.ValueKind == JsonValueKind.Array)
                            {
                                var outerArr = doc.RootElement.EnumerateArray().FirstOrDefault();
                                if (outerArr.ValueKind == JsonValueKind.Array)
                                {
                                    var sb = new StringBuilder();
                                    foreach (var innerArr in outerArr.EnumerateArray())
                                    {
                                        if (innerArr.ValueKind == JsonValueKind.Array)
                                        {
                                           var segment = innerArr.EnumerateArray().FirstOrDefault().GetString();
                                           if (!string.IsNullOrEmpty(segment)) sb.Append(segment);
                                        }
                                    }
                                    
                                    var trans = sb.ToString();
                                    if (!string.IsNullOrEmpty(trans))
                                    {
                                        results[idx] = trans;
                                        _memoryCache.TryAdd($"{targetLang}:{text}", trans);
                                        return;
                                    }
                                }
                            }
                        }
                    }
                    catch { }
                    // Keep original if failed
                    results[idx] = texts[idx];
                });
                
                await Task.WhenAll(tasks);
            }

            return results;
        }
    }
}
