using System;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace DebugTool
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("Starting News Logic Verification...");
            var handler = new HttpClientHandler { AllowAutoRedirect = true };
            using var client = new HttpClient(handler);
            client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");

            try 
            {
                // 1. Fetch RSS
                var location = "Surat";
                var query = Uri.EscapeDataString(location);
                var rssUrl = $"https://news.google.com/rss/search?q={query}&hl=en-US&gl=US&ceid=US:en";
                Console.WriteLine($"Fetching RSS: {rssUrl}");
                
                var rssXml = await client.GetStringAsync(rssUrl);
                var doc = XDocument.Parse(rssXml);
                var items = doc.Descendants("item").Take(15).Select(x => new {
                    Title = x.Element("title")?.Value,
                    Link = x.Element("link")?.Value
                }).ToList();

                Console.WriteLine($"Found {items.Count} items. Starting Parallel Deep Resolution...");
                
                var sw = Stopwatch.StartNew();
                
                var tasks = items.Select(async item => 
                {
                    Console.WriteLine($"[START] {item.Title.Substring(0, 10)}...");
                    try 
                    {
                        var realUrl = await ResolveGoogleRedirectAsync(item.Link);
                        if (!string.IsNullOrEmpty(realUrl))
                        {
                            var img = await ExtractOgImageAsync(realUrl);
                            Console.WriteLine($"[SUCCESS] {item.Title.Substring(0, 10)}... -> {img}");
                        }
                        else
                        {
                            Console.WriteLine($"[FAIL-RESOLVE] {item.Title.Substring(0, 10)}...");
                        }
                    }
                    catch (Exception ex)
                    {
                         Console.WriteLine($"[ERROR] {item.Title.Substring(0, 10)}... {ex.Message}");
                    }
                });

                await Task.WhenAll(tasks);
                sw.Stop();
                
                Console.WriteLine($"All tasks finished in {sw.Elapsed.TotalSeconds:F2}s");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"FATAL: {ex}");
            }
        }

        static async Task<string> ResolveGoogleRedirectAsync(string googleUrl)
        {
            try 
            {
                var handler = new HttpClientHandler { AllowAutoRedirect = true };
                handler.CookieContainer = new CookieContainer();
                handler.CookieContainer.Add(new Uri("https://news.google.com"), new Cookie("CONSENT", "YES+"));
                using var client = new HttpClient(handler);
                client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
                client.Timeout = TimeSpan.FromSeconds(5); 

                var html = await client.GetStringAsync(googleUrl);
                
                // Base64 Scanner Logic (Same as ExternalApiService)
                var candidates = Regex.Matches(html, "[\"']([A-Za-z0-9\\-_]{40,})[\"']");
                foreach (Match m in candidates)
                {
                    try 
                    {
                        var b64 = m.Groups[1].Value.Replace('-', '+').Replace('_', '/');
                        while (b64.Length % 4 != 0) b64 += "=";
                        var bytes = Convert.FromBase64String(b64);
                        var decoded = System.Text.Encoding.UTF8.GetString(bytes);
                        
                        var urlMatch = Regex.Match(decoded, @"(https?://[\w\-\.]+(:\d+)?(/[\w\-_\.\~\!\*\'\(\);\:@&=\+\$\,\%\?\#\[\]]*)?)");
                        if (urlMatch.Success && urlMatch.Value.Length > 15 && !urlMatch.Value.Contains("google.com"))
                        {
                            return urlMatch.Value;
                        }
                    }
                    catch {}
                }
            }
            catch {}
            return "";
        }

        static async Task<string> ExtractOgImageAsync(string articleUrl)
        {
            try 
            {
                var handler = new HttpClientHandler { AllowAutoRedirect = true };
                using var client = new HttpClient(handler);
                client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
                client.Timeout = TimeSpan.FromSeconds(5); 

                var html = await client.GetStringAsync(articleUrl);
                var match = Regex.Match(html, "<meta[^>]+property=[\"']og:image[\"'][^>]+content=[\"']([^\"']+)[\"']", RegexOptions.IgnoreCase);
                if (match.Success) return System.Net.WebUtility.HtmlDecode(match.Groups[1].Value);
            }
            catch {}
            return "";
        }
    }
}
