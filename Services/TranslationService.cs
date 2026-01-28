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

        public async Task<TranslationResult[]> TranslateBatchAsync(string[] texts, string targetLang, string sourceLang = "auto")
        {
            if (texts == null || texts.Length == 0) return Array.Empty<TranslationResult>();
            
            // Map inputs to results
            var results = texts.Select(t => new TranslationResult { OriginalText = t, TranslatedText = t, DetectedLanguage = sourceLang }).ToArray();

            if (string.IsNullOrEmpty(targetLang) || (targetLang.ToLower() == "en" && (string.IsNullOrEmpty(sourceLang) || sourceLang.ToLower() == "auto"))) 
            {
                return results;
            }

            var missingIndices = new List<int>();
            var missingTexts = new List<string>();

            // 1. Check Cache
            for (int i = 0; i < texts.Length; i++)
            {
                var text = texts[i];
                if (string.IsNullOrWhiteSpace(text)) continue;

                string key = $"{sourceLang}:{targetLang}:{text}";
                if (_memoryCache.TryGetValue(key, out var cached))
                {
                    // Cache format expectation: "DetectedLang|TranslatedText" or just "TranslatedText"
                    // For backward compatibility, if no pipe, assume sourceLang/auto
                    if (cached.Contains("|"))
                    {
                        var parts = cached.Split('|', 2);
                        results[i].DetectedLanguage = parts[0];
                        results[i].TranslatedText = parts[1];
                    }
                    else
                    {
                        results[i].TranslatedText = cached;
                    }
                }
                else
                {
                    missingIndices.Add(i);
                    missingTexts.Add(text);
                }
            }

            if (missingTexts.Count == 0) return results;

            // 2. Prepare API Request
            object requestObj;
            if (missingTexts.Count == 1)
            {
                requestObj = new
                {
                    q = missingTexts[0], // Send as single string to get detectedLanguage in response
                    source = sourceLang,
                    target = targetLang,
                    format = "text"
                };
            }
            else
            {
                requestObj = new
                {
                    q = missingTexts.ToArray(),
                    source = sourceLang,
                    target = targetLang,
                    format = "text"
                };
            }

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
                    using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(10));
                    var response = await _httpClient.PostAsync(url, content, cts.Token);
                    
                    if (!response.IsSuccessStatusCode) continue;

                    var responseJson = await response.Content.ReadAsStringAsync(cts.Token);
                    using var doc = JsonDocument.Parse(responseJson);

                    // LibreTranslate Response: { "translatedText": "..." } or { "translatedText": [ ... ], "detectedLanguage": { "language": "en" }?? No, usually detectedLanguage is per item or global? }
                    // Actually LibreTranslate batch returns array of string OR array of objects if detailed?
                    // Standard LibreTranslate /translate returns { "translatedText": "...", "detectedLanguage": { "confidence": 0, "language": "en" } }
                    // Let's assume text for now. If we want detection, we might need non-batch or check docs. 
                    // Batch endpoint usually just returns text strings. 
                    // For "Auto", we really want the detected language.
                    // If we use /translate (single) we get it. If we use batch, maybe not.
                    // Let's use single calls if "auto" is selected? Or just accept that batch might not return distinct languages per item easily without complex parsing.
                    // CAUTION: LibreTranslate /translate response:
                    // { "detectedLanguage": { "confidence": 87, "language": "en" }, "translatedText": "Hola" }
                    
                    // If we receive an array of strings, we don't get detected language.
                    // We need to check if the response has "detectedLanguage" property.
                    
                    if (doc.RootElement.TryGetProperty("translatedText", out var translatedEl))
                    {
                        // Single Item Response
                         if (translatedEl.ValueKind == JsonValueKind.String && missingIndices.Count == 1)
                         {
                             var val = translatedEl.GetString();
                             string detected = sourceLang;
                             if (doc.RootElement.TryGetProperty("detectedLanguage", out var detObj))
                             {
                                 // Log the object for debug
                                 Console.WriteLine($"[LibreTranslate] Detection Object: {detObj}");
                                 if (detObj.ValueKind == JsonValueKind.Object && detObj.TryGetProperty("language", out var langVal))
                                 {
                                     detected = langVal.GetString();
                                 }
                             }
                             
                             // CRITICAL: If source was auto and we didn't get detection, DO NOT Accept this result.
                             // Fallback to Google which implies detection.
                             if (sourceLang == "auto" && detected == "auto")
                             {
                                  Console.WriteLine("[LibreTranslate] Source is auto but no language detected. Falling back to Google.");
                                  continue; // Try next mirror or fallback
                             }

                             results[missingIndices[0]].TranslatedText = val;
                             results[missingIndices[0]].DetectedLanguage = detected;
                             success = true;
                             break;
                         }
                         else if (translatedEl.ValueKind == JsonValueKind.Array)
                         {
                             // It's just a string array
                             var translations = translatedEl.EnumerateArray().Select(x => x.GetString()).ToArray();
                             for (int i = 0; i < translations.Length; i++)
                             {
                                 if (i >= missingIndices.Count) break;
                                 var val = translations[i];
                                 results[missingIndices[i]].TranslatedText = val;
                                 // We don't get detected lang here easily unless api supports it
                                 if (!string.IsNullOrEmpty(val)) _memoryCache.TryAdd($"{sourceLang}:{targetLang}:{missingTexts[i]}", $"{sourceLang}|{val}");
                             }
                             success = true;
                             break;
                         }
                    }
                    else if (doc.RootElement.ValueKind == JsonValueKind.Array)
                    {
                         // Simple array response
                         var translations = doc.RootElement.EnumerateArray().Select(x => x.GetString()).ToArray();
                         for (int i = 0; i < translations.Length; i++)
                         {
                             if (i >= missingIndices.Count) break;
                             var val = translations[i];
                             results[missingIndices[i]].TranslatedText = val;
                             if (!string.IsNullOrEmpty(val)) _memoryCache.TryAdd($"{sourceLang}:{targetLang}:{missingTexts[i]}", $"{sourceLang}|{val}");
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

            // 4. Fallback: Google Translate
            if (!success)
            {
                var tasks = missingIndices.Select(async idx => 
                {
                    try 
                    {
                        var text = texts[idx];
                        var url = $"https://translate.googleapis.com/translate_a/single?client=gtx&sl={sourceLang}&tl={targetLang}&dt=t&q={Uri.EscapeDataString(text)}";
                        
                        var response = await _httpClient.GetAsync(url);
                        if (response.IsSuccessStatusCode)
                        {
                            var jsonStr = await response.Content.ReadAsStringAsync();
                            // Debug Log
                            Console.WriteLine($"[GoogleTranslate] Raw: {jsonStr}");

                            using var doc = JsonDocument.Parse(jsonStr);
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
                                    
                                    // Robust Parsing for Language Code
                                    // Google usually returns it as a string element in the root array, typically at index 2, but sometimes later.
                                    // We search for the first string element that looks like a language code (2-3 chars) in the root array, skipping the first element (sentences).
                                    string detected = sourceLang;
                                    int arrIdx = 0;
                                    foreach(var el in doc.RootElement.EnumerateArray())
                                    {
                                        // Skip the first element (chunks)
                                        if (arrIdx > 0 && el.ValueKind == JsonValueKind.String)
                                        {
                                            var possibleLang = el.GetString();
                                            // Simple validation: 2-3 chars (en, fr, gu, hi)
                                            if (!string.IsNullOrEmpty(possibleLang) && possibleLang.Length >= 2 && possibleLang.Length <= 5) 
                                            {
                                                detected = possibleLang;
                                                break;
                                            }
                                        }
                                        arrIdx++;
                                    }

                                    if (!string.IsNullOrEmpty(trans))
                                    {
                                        results[idx].TranslatedText = trans;
                                        results[idx].DetectedLanguage = detected;
                                        _memoryCache.TryAdd($"{sourceLang}:{targetLang}:{text}", $"{detected}|{trans}");
                                        return;
                                    }
                                }
                            }
                        }
                    }
                    catch { }
                });
                
                await Task.WhenAll(tasks);
            }

            return results;
        }
    }
}
