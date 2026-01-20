using System;
using System.Net.Http;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Xml.Linq;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using TodoListApp.ViewModels;

namespace TodoListApp.Services
{
    public class ExternalApiService : IExternalApiService
    {
        private readonly HttpClient _httpClient;
        private static List<NewsItem> _newsCache = new List<NewsItem>();
        private static JsonDocument? _tempCache;

        public ExternalApiService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public List<NewsItem> GetCachedNews() => _newsCache;

        public async Task<WeatherData> GetWeatherAsync(string city)
        {
            try
            {
                var geoUrl = $"https://geocoding-api.open-meteo.com/v1/search?name={city}&count=1&language=en&format=json";
                var geoResponse = await _httpClient.GetAsync(geoUrl);
                geoResponse.EnsureSuccessStatusCode();
                var geoJson = await geoResponse.Content.ReadAsStringAsync();
                
                using var geoDoc = JsonDocument.Parse(geoJson);
                var root = geoDoc.RootElement;
                if (!root.TryGetProperty("results", out var results) || results.GetArrayLength() == 0) 
                    return new WeatherData { City = city, Description = "City not found" };

                var result = results[0];
                var lat = result.GetProperty("latitude").GetDouble();
                var lon = result.GetProperty("longitude").GetDouble();
                
                var name = result.GetProperty("name").GetString() ?? "";
                var admin4 = result.TryGetProperty("admin4", out var a4) ? a4.GetString() : ""; // Village
                var admin3 = result.TryGetProperty("admin3", out var a3) ? a3.GetString() : ""; // Town/City
                var admin2 = result.TryGetProperty("admin2", out var a2) ? a2.GetString() : ""; // District
                var admin1 = result.TryGetProperty("admin1", out var a1) ? a1.GetString() : ""; // State
                var country = result.TryGetProperty("country", out var c) ? c.GetString() : "";

                var data = await FetchWeatherDataAsync(lat, lon, name);
                
                // Prioritize naming: Village > Town > City
                data.LocalArea = !string.IsNullOrEmpty(admin4) ? admin4 : (name != admin3 ? name : "");
                data.City = !string.IsNullOrEmpty(admin3) ? admin3 : name;
                data.District = admin2 ?? "";
                data.State = admin1 ?? "";
                data.Country = country ?? "";
                
                // If name is a PIN/Number, use district/state as City
                if (int.TryParse(name, out _)) {
                    data.City = !string.IsNullOrEmpty(admin3) ? admin3 : (!string.IsNullOrEmpty(admin2) ? admin2 : name);
                }
                
                return data;
            }
            catch
            {
                return new WeatherData { City = city, Description = "Service unavailable", Temperature = 0 };
            }
        }

        public async Task<WeatherData> GetWeatherByCoordsAsync(double lat, double lon)
        {
            string cityName = "My Location";
            string state = "";
            string country = "";
            string localArea = "";
            string district = "";
            
            try
            {
                var revGeoUrl = $"https://api.bigdatacloud.net/data/reverse-geocode-client?latitude={lat}&longitude={lon}&localityLanguage=en";
                var response = await _httpClient.GetAsync(revGeoUrl);
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(json);
                    var root = doc.RootElement;
                    
                    localArea = root.TryGetProperty("locality", out var l) ? l.GetString() ?? "" : "";
                    cityName = root.TryGetProperty("city", out var c) ? c.GetString() ?? "" : "";
                    state = root.TryGetProperty("principalSubdivision", out var s) ? s.GetString() ?? "" : "";
                    country = root.TryGetProperty("countryName", out var cn) ? cn.GetString() ?? "" : "";

                    // Extract District from localityInfo if possible
                    if (root.TryGetProperty("localityInfo", out var info) && info.TryGetProperty("administrative", out var admin)) {
                        foreach (var item in admin.EnumerateArray()) {
                            var order = item.TryGetProperty("order", out var o) ? o.GetInt32() : 0;
                            if (order == 6 || order == 5) { // Common orders for districts/counties
                                district = item.GetProperty("name").GetString() ?? "";
                            }
                        }
                    }
                }
            }
            catch { }

            try
            {
                var data = await FetchWeatherDataAsync(lat, lon, cityName ?? localArea ?? "My Location");
                data.LocalArea = localArea ?? "";
                data.City = !string.IsNullOrEmpty(cityName) ? cityName : (localArea ?? "My Location");
                data.District = district ?? "";
                data.State = state ?? "";
                data.Country = country ?? "";
                
                return data;
            }
            catch
            {
                return new WeatherData { City = cityName ?? localArea ?? "My Location", Description = "Service unavailable", Temperature = 0 };
            }
        }

        private async Task<WeatherData> FetchWeatherDataAsync(double lat, double lon, string cityName)
        {
            var weatherTask = _httpClient.GetStringAsync($"https://api.open-meteo.com/v1/forecast?latitude={lat}&longitude={lon}&current=temperature_2m,relative_humidity_2m,apparent_temperature,weather_code,pressure_msl,wind_speed_10m,wind_direction_10m,visibility,dew_point_2m&daily=temperature_2m_max,temperature_2m_min,weather_code,sunrise,sunset,uv_index_max&timezone=auto");
            var aqTask = _httpClient.GetStringAsync($"https://air-quality-api.open-meteo.com/v1/air-quality?latitude={lat}&longitude={lon}&current=us_aqi");
            var moonTask = _httpClient.GetStringAsync($"https://wttr.in/{lat},{lon}?format=j1");

            try { await Task.WhenAll(weatherTask); } catch { throw; } 

            var weatherJson = await weatherTask;
            using var weatherDoc = JsonDocument.Parse(weatherJson);
            var current = weatherDoc.RootElement.GetProperty("current");
            var daily = weatherDoc.RootElement.GetProperty("daily");

            var weatherData = new WeatherData
            {
                City = cityName,
                Latitude = lat,
                Longitude = lon,
                Temperature = current.GetProperty("temperature_2m").GetDouble(),
                FeelsLike = current.GetProperty("apparent_temperature").GetDouble(),
                Humidity = current.GetProperty("relative_humidity_2m").GetInt32(),
                WindSpeed = current.GetProperty("wind_speed_10m").GetDouble(),
                WindDirection = current.GetProperty("wind_direction_10m").GetInt32(),
                Pressure = current.GetProperty("pressure_msl").GetDouble(),
                Visibility = current.GetProperty("visibility").GetDouble() / 1000.0,
                DewPoint = current.GetProperty("dew_point_2m").GetDouble(),
                Description = GetWeatherDescription(current.GetProperty("weather_code").GetInt32()),
                Icon = GetWeatherIcon(current.GetProperty("weather_code").GetInt32()),
                Sunrise = daily.GetProperty("sunrise")[0].GetString()?.Split('T').Last() ?? "",
                Sunset = daily.GetProperty("sunset")[0].GetString()?.Split('T').Last() ?? "",
                UVIndex = daily.GetProperty("uv_index_max")[0].GetDouble()
            };
            weatherData.UVRisk = GetUVRiskLevel(weatherData.UVIndex);

            try {
                var aqJson = await aqTask;
                using var aqDoc = JsonDocument.Parse(aqJson);
                weatherData.AQI = aqDoc.RootElement.GetProperty("current").GetProperty("us_aqi").GetInt32();
                weatherData.AQILevel = GetAQILevel(weatherData.AQI);
            } catch { }

            try {
                var moonJson = await moonTask;
                using var moonDoc = JsonDocument.Parse(moonJson);
                var moonAstronomy = moonDoc.RootElement.GetProperty("weather")[0].GetProperty("astronomy")[0];
                weatherData.Moonrise = moonAstronomy.GetProperty("moonrise").GetString() ?? "";
                weatherData.Moonset = moonAstronomy.GetProperty("moonset").GetString() ?? "";
                weatherData.MoonPhase = moonAstronomy.GetProperty("moon_phase").GetString() ?? "";
                weatherData.MoonIcon = GetMoonIcon(weatherData.MoonPhase);
            } catch { }

            var dates = daily.GetProperty("time").EnumerateArray().ToList();
            var maxTemps = daily.GetProperty("temperature_2m_max").EnumerateArray().ToList();
            var minTemps = daily.GetProperty("temperature_2m_min").EnumerateArray().ToList();
            var codes = daily.GetProperty("weather_code").EnumerateArray().ToList();

            for (int i = 1; i < dates.Count && i <= 5; i++)
            {
                var dateStr = dates[i].GetString();
                if (DateTime.TryParse(dateStr, out var date))
                {
                    weatherData.Forecasts.Add(new DailyForecast
                    {
                        Date = date.ToString("ddd"),
                        MaxTemp = maxTemps[i].GetDouble(),
                        MinTemp = minTemps[i].GetDouble(),
                        Description = GetWeatherDescription(codes[i].GetInt32()),
                        Icon = GetWeatherIcon(codes[i].GetInt32())
                    });
                }
            }

            return weatherData;
        }

        public async Task<CurrencyConversionData> GetCurrencyRateAsync(string from, string to)
        {
            try
            {
                // Fetch latest and previous day to calculate movement
                var yesterday = DateTime.UtcNow.AddDays(-1).ToString("yyyy-MM-dd");
                var latestUrl = $"https://api.frankfurter.app/latest?amount=1&from={from}&to={to}";
                var yesterdayUrl = $"https://api.frankfurter.app/{yesterday}?from={from}&to={to}";

                var latestTask = _httpClient.GetStringAsync(latestUrl);
                var yesterdayTask = _httpClient.GetStringAsync(yesterdayUrl);

                await Task.WhenAll(latestTask);
                
                var latestJson = await latestTask;
                using var latestDoc = JsonDocument.Parse(latestJson);
                var latestRate = latestDoc.RootElement.GetProperty("rates").GetProperty(to).GetDecimal();

                bool? isUp = null;
                try {
                    var yesterdayJson = await yesterdayTask;
                    using var yesterdayDoc = JsonDocument.Parse(yesterdayJson);
                    var yesterdayRate = yesterdayDoc.RootElement.GetProperty("rates").GetProperty(to).GetDecimal();
                    isUp = latestRate >= yesterdayRate;
                } catch { }

                return new CurrencyConversionData
                {
                    From = from,
                    To = to,
                    Rate = latestRate,
                    ConvertedAmount = latestRate,
                    LastUpdated = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss") + " UTC",
                    IsUp = isUp
                };
            }
            catch
            {
                return new CurrencyConversionData { From = from, To = to, Rate = 0, ConvertedAmount = 0 };
            }
        }

        public async Task<TimeData> GetTimeConversionAsync(string sourceZoneId, string targetZoneId, string? customTime = null)
        {
            await Task.CompletedTask; // Satisfy async requirement
            try
            {
                TimeZoneInfo sourceZone = GetTimeZone(sourceZoneId);
                TimeZoneInfo targetZone = GetTimeZone(targetZoneId);
                
                DateTime baseTime;
                if (!string.IsNullOrEmpty(customTime) && DateTime.TryParse(customTime, out var dt))
                {
                    baseTime = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(dt, DateTimeKind.Unspecified), sourceZone);
                }
                else
                {
                    baseTime = DateTime.UtcNow;
                }

                var sourceTime = TimeZoneInfo.ConvertTimeFromUtc(baseTime, sourceZone);
                var targetTime = TimeZoneInfo.ConvertTimeFromUtc(baseTime, targetZone);
                var offset = targetZone.GetUtcOffset(baseTime) - sourceZone.GetUtcOffset(baseTime);

                string dayChange = "Same Day";
                if (targetTime.Date > sourceTime.Date) dayChange = "Next Day";
                else if (targetTime.Date < sourceTime.Date) dayChange = "Previous Day";

                var absMinutes = Math.Abs((int)offset.TotalMinutes);
                string offsetDisplay = $"{(offset.TotalMinutes >= 0 ? "+" : "-")}{absMinutes / 60:D2}:{absMinutes % 60:D2}";

                // Helper to get abbreviation (simple extraction from ID or DisplayName)
                string getAbbr(TimeZoneInfo tz, DateTime dt) {
                    var idParts = tz.Id.Split(' ');
                    if (idParts.Length > 1 && idParts.Last().Length <= 4) return idParts.Last(); 
                    // Fallback to a simplified version of the ID if it's a city name
                    var cityPart = tz.Id.Split('/').Last().Replace("_", " ");
                    return cityPart.Length <= 6 ? cityPart : "TZ"; 
                }

                return new TimeData
                {
                    SourceZone = sourceZone.Id,
                    TargetZone = targetZone.Id,
                    SourceTime = sourceTime.ToString("HH:mm"),
                    TargetTime = targetTime.ToString("HH:mm"),
                    TotalOffsetMinutes = offset.TotalMinutes,
                    DayChange = dayChange,
                    OffsetDisplay = offsetDisplay,
                    SourceAbbr = getAbbr(sourceZone, sourceTime),
                    TargetAbbr = getAbbr(targetZone, targetTime)
                };
            }
            catch 
            {
                return new TimeData { SourceZone = sourceZoneId, TargetZone = targetZoneId, SourceTime = "--:--", TargetTime = "--:--", TotalOffsetMinutes = 0, DayChange = "", OffsetDisplay = "", SourceAbbr = "", TargetAbbr = "" };
            }
        }

        public async Task<UserLocationData> GetLocationFromIpAsync(string ip)
        {
            if (string.IsNullOrEmpty(ip) || ip == "::1" || ip == "127.0.0.1")
                return new UserLocationData { City = "", TimeZoneId = "", Currency = "" };

            try
            {
                var response = await _httpClient.GetAsync($"http://ip-api.com/json/{ip}?fields=status,message,city,timezone,currency");
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(json);
                    var root = doc.RootElement;
                    if (root.GetProperty("status").GetString() == "success")
                    {
                        return new UserLocationData { 
                            City = root.TryGetProperty("city", out var c) ? c.GetString() ?? "London" : "London", 
                            TimeZoneId = root.TryGetProperty("timezone", out var tz) ? tz.GetString() ?? "UTC" : "UTC",
                            Currency = root.TryGetProperty("currency", out var curr) ? curr.GetString() ?? "USD" : "USD"
                        };
                    }
                }
            }
            catch { }
            return new UserLocationData { City = "London", TimeZoneId = "GMT Standard Time", Currency = "USD" };
        }

        public async Task<CurrencyHistoryData> GetCurrencyHistoryAsync(string from, string to, int days = 7)
        {
            try
            {
                var endDate = DateTime.UtcNow.ToString("yyyy-MM-dd");
                var startDate = DateTime.UtcNow.AddDays(-days).ToString("yyyy-MM-dd");
                var url = $"https://api.frankfurter.app/{startDate}..{endDate}?from={from}&to={to}";

                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();
                var json = await response.Content.ReadAsStringAsync();

                using var doc = JsonDocument.Parse(json);
                var rates = doc.RootElement.GetProperty("rates");
                
                var data = new CurrencyHistoryData();
                foreach (var day in rates.EnumerateObject())
                {
                    data.Labels.Add(day.Name);
                    if (day.Value.TryGetProperty(to, out var rateElement))
                        data.Values.Add(rateElement.GetDecimal());
                }
                return data;
            }
            catch { return new CurrencyHistoryData(); }
        }

        public async Task<string?> GetCurrencyFromLocationAsync(string location)
        {
            try
            {
                var url = $"https://geocoding-api.open-meteo.com/v1/search?name={Uri.EscapeDataString(location)}&count=1&language=en&format=json";
                var response = await _httpClient.GetAsync(url);
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(json);
                    var results = doc.RootElement.TryGetProperty("results", out var r) ? r : default;
                    if (results.ValueKind == JsonValueKind.Array && results.GetArrayLength() > 0)
                    {
                        var country = results[0].TryGetProperty("country", out var c) ? c.GetString() : null;
                        if (!string.IsNullOrEmpty(country))
                        {
                            // Map country name to currency code
                            foreach (var curr in TodoListApp.ViewModels.DashboardViewModel.StaticAvailableCurrencies)
                            {
                                var info = GetCurrencyInfo(curr);
                                if (info.Country.Equals(country, StringComparison.OrdinalIgnoreCase))
                                    return curr;
                            }
                        }
                    }
                }
            }
            catch { }
            return null;
        }

        public async Task<string?> GetTimeZoneByLocationAsync(string location)
        {
            try
            {
                var url = $"https://geocoding-api.open-meteo.com/v1/search?name={Uri.EscapeDataString(location)}&count=1&language=en&format=json";
                var response = await _httpClient.GetAsync(url);
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(json);
                    var results = doc.RootElement.TryGetProperty("results", out var r) ? r : default;
                    
                    if (results.ValueKind == JsonValueKind.Array && results.GetArrayLength() > 0)
                    {
                        // OpenMeteo returns 'timezone' field (e.g. "Asia/Kolkata")
                        return results[0].TryGetProperty("timezone", out var tz) ? tz.GetString() : null;
                    }
                }
            }
            catch { }
            return null;
        }
  private TimeZoneInfo GetTimeZone(string id)
        {
            try 
            {
                // 1. Try direct system ID (Windows)
                try { return TimeZoneInfo.FindSystemTimeZoneById(id); } catch { }

                // 2. Try IANA to Windows conversion
                if (TimeZoneInfo.TryConvertIanaIdToWindowsId(id, out var windowsId))
                {
                    return TimeZoneInfo.FindSystemTimeZoneById(windowsId);
                }

                // 3. Heuristic fallbacks
                if (id.Contains("Pacific")) return TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time");
                if (id.Contains("Eastern")) return TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
                if (id.Contains("GMT") || id.Contains("London")) return TimeZoneInfo.FindSystemTimeZoneById("GMT Standard Time");
                if (id.Contains("Tokyo")) return TimeZoneInfo.FindSystemTimeZoneById("Tokyo Standard Time");
                if (id.Contains("India") || id.Contains("Kolkata")) return TimeZoneInfo.FindSystemTimeZoneById("India Standard Time");
                
                return TimeZoneInfo.Utc;
            }
            catch
            {
                return TimeZoneInfo.Utc;
            }
        }

        private string GetAQILevel(int aqi) => aqi switch {
            <= 50 => "Good", <= 100 => "Moderate", <= 150 => "Unhealthy for Sensitive Groups",
            <= 200 => "Unhealthy", <= 300 => "Very Unhealthy", _ => "Hazardous"
        };

        private string GetUVRiskLevel(double uv) => uv switch {
            < 3 => "Low", < 6 => "Moderate", < 8 => "High", < 11 => "Very High", _ => "Extreme"
        };

        private string GetMoonIcon(string phase) => phase.ToLower() switch {
            var p when p.Contains("new") => "🌑", var p when p.Contains("waxing crescent") => "🌒",
            var p when p.Contains("first quarter") => "🌓", var p when p.Contains("waxing gibbous") => "🌔",
            var p when p.Contains("full") => "🌕", var p when p.Contains("waning gibbous") => "🌖",
            var p when p.Contains("last quarter") => "🌗", var p when p.Contains("waning crescent") => "🌘",
            _ => "🌙"
        };

        private string GetWeatherDescription(int code) => code switch {
            0 => "Clear sky", 1 or 2 or 3 => "Partly cloudy", 45 or 48 => "Fog",
            51 or 53 or 55 => "Drizzle", 61 or 63 or 65 => "Rain", 71 or 73 or 75 => "Snow",
            95 or 96 or 99 => "Thunderstorm", _ => "Unknown"
        };

        private string GetWeatherIcon(int code) => code switch {
            0 => "☀️", 1 or 2 or 3 => "⛅", 45 or 48 => "🌫️", 51 or 53 or 55 => "🌦️",
            61 or 63 or 65 => "🌧️", 71 or 73 or 75 => "❄️", 95 or 96 or 99 => "⚡", _ => "🌡️"
        };

        public static string GetCurrencyFlag(string currencyCode) => GetCurrencyInfo(currencyCode).Flag;

        public static (string Flag, string Country, string Name) GetCurrencyInfo(string code) => code.ToUpper() switch {
            "USD" => ("🇺🇸", "United States", "US Dollar"),
            "EUR" => ("🇪🇺", "Eurozone", "Euro"),
            "GBP" => ("🇬🇧", "United Kingdom", "British Pound"),
            "JPY" => ("🇯🇵", "Japan", "Japanese Yen"),
            "AUD" => ("🇦🇺", "Australia", "Australian Dollar"),
            "CAD" => ("🇨🇦", "Canada", "Canadian Dollar"),
            "CHF" => ("🇨🇭", "Switzerland", "Swiss Franc"),
            "CNY" => ("🇨🇳", "China", "Chinese Yuan"),
            "HKD" => ("🇭🇰", "Hong Kong", "Hong Kong Dollar"),
            "NZD" => ("🇳🇿", "New Zealand", "New Zealand Dollar"),
            "INR" => ("🇮🇳", "India", "Indian Rupee"),
            "BRL" => ("🇧🇷", "Brazil", "Brazilian Real"),
            "RUB" => ("🇷🇺", "Russia", "Russian Ruble"),
            "KRW" => ("🇰🇷", "South Korea", "South Korean Won"),
            "MXN" => ("🇲🇽", "Mexico", "Mexican Peso"),
            "SGD" => ("🇸🇬", "Singapore", "Singapore Dollar"),
            "THB" => ("🇹🇭", "Thailand", "Thai Baht"),
            "TRY" => ("🇹🇷", "Turkey", "Turkish Lira"),
            "ZAR" => ("🇿🇦", "South Africa", "South African Rand"),
            "ILS" => ("🇮🇱", "Israel", "Israeli New Shekel"),
            "PHP" => ("🇵🇭", "Philippines", "Philippine Peso"),
            "MYR" => ("🇲🇾", "Malaysia", "Malaysian Ringgit"),
            "IDR" => ("🇮🇩", "Indonesia", "Indonesian Rupiah"),
            "CZK" => ("🇨🇿", "Czech Republic", "Czech Koruna"),
            "HUF" => ("🇭🇺", "Hungary", "Hungarian Forint"),
            "PLN" => ("🇵🇱", "Poland", "Polish Zloty"),
            "RON" => ("🇷🇴", "Romania", "Romanian Leu"),
            "SEK" => ("🇸🇪", "Sweden", "Swedish Krona"),
            "ISK" => ("🇮🇸", "Iceland", "Icelandic Krona"),
            "NOK" => ("🇳🇴", "Norway", "Norwegian Krone"),
            "HRK" => ("🇭🇷", "Croatia", "Croatian Kuna"),
            "BGN" => ("🇧🇬", "Bulgaria", "Bulgarian Lev"),
            "DKK" => ("🇩🇰", "Denmark", "Danish Krone"),
            "AED" => ("🇦🇪", "United Arab Emirates", "UAE Dirham"),
            "SAR" => ("🇸🇦", "Saudi Arabia", "Saudi Riyal"),
            _ => ("🏳️", "Unknown", "Currency")
        };

        public async Task<NewsData> GetNewsAsync(string location, string? category = null, string sortBy = "relevance")
        {
            try
            {
                // 1. Resolve Location (Geocoding fallback)
                var geoUrl = $"https://geocoding-api.open-meteo.com/v1/search?name={Uri.EscapeDataString(location)}&count=1&language=en&format=json";
                var geoRes = await _httpClient.GetAsync(geoUrl);
                var geoJson = await geoRes.Content.ReadAsStringAsync();
                
                string resolvedName = location;
                string countryCode = "US";
                
                using (var doc = JsonDocument.Parse(geoJson))
                {
                    if (doc.RootElement.TryGetProperty("results", out var results) && results.GetArrayLength() > 0)
                    {
                        var res = results[0];
                        var city = res.TryGetProperty("admin3", out var a3) ? a3.GetString() : "";
                        var district = res.TryGetProperty("admin2", out var a2) ? a2.GetString() : "";
                        var country = res.TryGetProperty("country_code", out var cc) ? cc.GetString() : "US";
                        
                        resolvedName = !string.IsNullOrEmpty(city) ? city : (!string.IsNullOrEmpty(district) ? district : res.GetProperty("name").GetString() ?? location);
                        countryCode = country ?? "US";
                    }
                }

                // 2. Build Query
                var queryBuilder = new System.Text.StringBuilder($"{resolvedName}");
                if (!string.IsNullOrEmpty(category) && category != "All")
                {
                    queryBuilder.Append($" {category}");
                }

                // Append local context if searching for village/small area
                if (!location.Equals(resolvedName, StringComparison.OrdinalIgnoreCase))
                {
                    queryBuilder.Append($" OR {location}");
                }

                var finalQuery = Uri.EscapeDataString(queryBuilder.ToString());
                var rssUrl = $"https://news.google.com/rss/search?q={finalQuery}&hl=en-{countryCode}&gl={countryCode}&ceid={countryCode}:en";

                // 3. Fetch and Parse RSS
                var rssRes = await _httpClient.GetAsync(rssUrl);
                var rssXml = await rssRes.Content.ReadAsStringAsync();
                var xdoc = XDocument.Parse(rssXml);
                XNamespace media = "http://search.yahoo.com/mrss/";

                var items = xdoc.Descendants("item").Select(item => {
                    var title = item.Element("title")?.Value ?? "";
                    var description = item.Element("description")?.Value ?? "";
                    var source = item.Element("source")?.Value ?? "News Source";
                    var pubDate = item.Element("pubDate")?.Value ?? "";
                    var link = item.Element("link")?.Value ?? "";

                    // Extract image from description (common in Google News RSS)
                    var imgMatch = Regex.Match(description, "<img src=\"([^\"]+)\"");
                    var imgUrl = imgMatch.Success ? imgMatch.Groups[1].Value : "";

                    // Try media:content if description photo is missing
                    if (string.IsNullOrEmpty(imgUrl))
                    {
                        var mediaContent = item.Element(media + "content");
                        if (mediaContent != null)
                        {
                            imgUrl = mediaContent.Attribute("url")?.Value ?? "";
                        }
                    }

                    // Try looking for direct thumbnail element
                    if (string.IsNullOrEmpty(imgUrl))
                    {
                        var mediaThumbnail = item.Element(media + "thumbnail");
                        if (mediaThumbnail != null)
                        {
                            imgUrl = mediaThumbnail.Attribute("url")?.Value ?? "";
                        }
                    }

                    // Clean title (often includes " - Source")
                    var cleanTitle = title;
                    if (title.Contains(" - ")) 
                    {
                        cleanTitle = title.Substring(0, title.LastIndexOf(" - ")).Trim();
                    }

                    // Format date
                    if (DateTime.TryParse(pubDate, out var dt))
                    {
                        pubDate = dt.ToString("MMM dd, yyyy HH:mm");
                    }

                    // Improve summary extraction: 
                    // 1. Remove specific unwanted HTML structures (like the table and images we already handled)
                    // 2. Extract meaningful text snippets
                    var cleanSummary = description;
                    if (description.Contains("<li>")) {
                        // Often Google News lists related articles in <li> tags, let's extract the main one or join them
                        var snippets = Regex.Matches(description, "<li>.*?<a.*?>(.*?)</a>")
                                            .Select(m => m.Groups[1].Value)
                                            .ToList();
                        if (snippets.Any()) cleanSummary = string.Join(". ", snippets);
                    }
                    
                    cleanSummary = Regex.Replace(cleanSummary, "<.*?>", "").Trim();
                    if (cleanSummary.Length > 300) cleanSummary = cleanSummary.Substring(0, 297) + "...";

                    return new NewsItem {
                        Title = cleanTitle,
                        Description = cleanSummary,
                        Source = source,
                        Published = pubDate,
                        Link = link,
                        ImageUrl = imgUrl,
                        Category = category ?? "General"
                    };
                }).ToList();

                // 4. Sort if requested
                if (sortBy == "latest")
                {
                    items = items.OrderByDescending(i => {
                        DateTime.TryParse(i.Published, out var dt);
                        return dt;
                    }).ToList();
                }

                var finalItems = items.Take(100).ToList();
                _newsCache = finalItems; // Update cache

                return new NewsData {
                    Items = finalItems,
                    ResolvedLocation = resolvedName
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"News Error: {ex.Message}");
                return new NewsData { ResolvedLocation = location };
            }
        }

        public Task<string> GetNewsDetailAsync(string url)
        {
            // Direct scraping is now disabled per user request for authenticity and compliance.
            // Content is now served via extended summaries passed from the RSS feed directly.
            return Task.FromResult("This article is served as an authenticated summary. Please click the 'Read More' link to view the full story on the publisher's website if needed.");
        }

        public async Task<List<CountryData>> SearchCountriesAsync(string query)
        {
            if (string.IsNullOrWhiteSpace(query)) return new List<CountryData>();

            try
            {
                var response = await _httpClient.GetAsync($"https://restcountries.com/v3.1/name/{Uri.EscapeDataString(query)}?fields=name,flags,cca2,capital,region,subregion,continents,population,area,languages,currencies,timezones,borders,maps,flag");
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(json);
                    return doc.RootElement.EnumerateArray().Select(MapToCountryData).ToList();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Country Search Error: {ex.Message}");
            }
            return new List<CountryData>();
        }

        public async Task<CountryData?> GetCountryDetailsAsync(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;

            try
            {
                // Use full text search for exact name
                var response = await _httpClient.GetAsync($"https://restcountries.com/v3.1/name/{Uri.EscapeDataString(name)}?fullText=true&fields=name,flags,cca2,capital,region,subregion,continents,population,area,languages,currencies,timezones,borders,maps,flag,latlng,idd,tld,car");
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(json);
                    var results = doc.RootElement.EnumerateArray().ToList();
                    if (results.Any())
                    {
                        var data = MapToCountryData(results[0]);
                        // Enrich with Wikipedia
                        await EnrichWithWikipedia(data);
                        // Enrich with GDP
                        await EnrichWithGDP(data);
                        // Enrich with Electricity
                        EnrichWithElectricity(data);
                        // Enrich with Climate
                        await EnrichWithClimate(data);
                        return data;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Country Details Error: {ex.Message}");
            }
            return null;
        }

        private async Task EnrichWithClimate(CountryData data)
        {
            try
            {
                if (_tempCache == null)
                {
                    var res = await _httpClient.GetAsync("https://raw.githubusercontent.com/samayo/country-json/master/src/country-by-yearly-average-temperature.json");
                    if (res.IsSuccessStatusCode)
                    {
                        var json = await res.Content.ReadAsStringAsync();
                        _tempCache = JsonDocument.Parse(json);
                    }
                }

                if (_tempCache != null)
                {
                    foreach (var item in _tempCache.RootElement.EnumerateArray())
                    {
                        var cName = item.GetProperty("country").GetString();
                        if (cName != null && (cName.Equals(data.Name, StringComparison.OrdinalIgnoreCase) || cName.Equals(data.OfficialName, StringComparison.OrdinalIgnoreCase)))
                        {
                            if (item.TryGetProperty("temperature", out var temp) && temp.ValueKind == JsonValueKind.Number)
                            {
                                data.AverageTemperature = temp.GetDouble().ToString("F1") + "°C (Annual Avg)";
                                break;
                            }
                        }
                    }
                }
            }
            catch { }
        }

        private async Task EnrichWithGDP(CountryData data)
        {
            try
            {
                // World Bank API for GDP (using ISO2 code)
                var response = await _httpClient.GetAsync($"http://api.worldbank.org/v2/country/{data.IsoCode}/indicator/NY.GDP.MKTP.CD?format=json&date=2021:2023");
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.GetArrayLength() > 1)
                    {
                        var list = doc.RootElement[1];
                        foreach (var item in list.EnumerateArray())
                        {
                            if (item.TryGetProperty("value", out var val) && val.ValueKind == JsonValueKind.Number)
                            {
                                double gdpValue = val.GetDouble();
                                if (gdpValue >= 1e12) data.GDP = $"${(gdpValue / 1e12):F2} Trillion";
                                else if (gdpValue >= 1e9) data.GDP = $"${(gdpValue / 1e9):F2} Billion";
                                else data.GDP = $"${(gdpValue / 1e6):F2} Million";
                                break;
                            }
                        }
                    }
                }
            }
            catch { }
        }

        private void EnrichWithElectricity(CountryData data)
        {
            // Basic regional/country specific electricity info
            if (data.IsoCode == "US" || data.IsoCode == "CA" || data.IsoCode == "MX") { data.ElectricityVoltage = "120V"; data.ElectricityPlugTypes = "A, B"; }
            else if (data.IsoCode == "JP") { data.ElectricityVoltage = "100V"; data.ElectricityPlugTypes = "A, B"; }
            else if (data.IsoCode == "GB" || data.IsoCode == "IE" || data.IsoCode == "HK" || data.IsoCode == "SG" || data.IsoCode == "MY") { data.ElectricityVoltage = "230V"; data.ElectricityPlugTypes = "G"; }
            else if (data.IsoCode == "AU" || data.IsoCode == "NZ") { data.ElectricityVoltage = "230V"; data.ElectricityPlugTypes = "I"; }
            else if (data.IsoCode == "IN") { data.ElectricityVoltage = "230V"; data.ElectricityPlugTypes = "C, D, M"; }
            else if (data.Region == "Europe") { data.ElectricityVoltage = "230V"; data.ElectricityPlugTypes = "C, E, F"; }
            else { data.ElectricityVoltage = "220-240V"; data.ElectricityPlugTypes = "C, G, M common"; }
        }

        private async Task EnrichWithWikipedia(CountryData data)
        {
            try
            {
                var wikiResponse = await _httpClient.GetAsync($"https://en.wikipedia.org/api/rest_v1/page/summary/{Uri.EscapeDataString(data.Name)}");
                if (wikiResponse.IsSuccessStatusCode)
                {
                    var wikiJson = await wikiResponse.Content.ReadAsStringAsync();
                    using var wikiDoc = JsonDocument.Parse(wikiJson);
                    var root = wikiDoc.RootElement;

                    if (root.TryGetProperty("extract", out var extract))
                    {
                        string summary = extract.GetString() ?? "";
                        data.WikipediaSummary = summary;
                        
                        // Extract Religions (heuristic)
                        if (summary.Contains("Muslim")) data.Religions = "Islam (Major)";
                        else if (summary.Contains("Christian")) data.Religions = "Christianity (Major)";
                        else if (summary.Contains("Hindu")) data.Religions = "Hinduism (Major)";
                        else if (summary.Contains("Buddhist")) data.Religions = "Buddhism (Major)";
                        
                        // Extract Industry mentions
                        if (summary.Contains("agriculture")) data.MajorIndustries = "Agriculture, Service";
                        else if (summary.EndsWith("technology", StringComparison.OrdinalIgnoreCase) || summary.Contains(" technology")) data.MajorIndustries = "Technology, Manufacturing";
                        else if (summary.Contains("oil")) data.MajorIndustries = "Petroleum, Gas";

                        // Climate Type heuristic
                        if (summary.Contains("tropical")) data.ClimateType = "Tropical";
                        else if (summary.Contains("temperate")) data.ClimateType = "Temperate";
                        else if (summary.Contains("desert")) data.ClimateType = "Arid / Desert";
                        else if (summary.Contains("mediterranean")) data.ClimateType = "Mediterranean";
                        else if (summary.Contains("continental")) data.ClimateType = "Continental";

                        // Natural Resources heuristic
                        var resources = new List<string>();
                        if (summary.Contains("gold")) resources.Add("Gold");
                        if (summary.Contains("oil")) resources.Add("Oil");
                        if (summary.Contains("gas")) resources.Add("Natural Gas");
                        if (summary.Contains("timber")) resources.Add("Timber");
                        if (summary.Contains("minerals")) resources.Add("Minerals");
                        if (resources.Any()) data.NaturalResources = string.Join(", ", resources);

                        // Famous Places (Search for capitalized names that are likely cities/places)
                        var places = new List<string>();
                        if (summary.Contains("UNESCO")) data.GlobalRankings["UNESCO Sites"] = "Multiple";
                        
                        // Organizations
                        if (summary.Contains("UN ")) data.InternationalOrganizations.Add("UN");
                        if (summary.Contains("WHO ")) data.InternationalOrganizations.Add("WHO");
                        if (summary.Contains("EU ")) data.InternationalOrganizations.Add("EU");
                        if (summary.Contains("NATO")) data.InternationalOrganizations.Add("NATO");
                        if (summary.Contains("G20")) data.InternationalOrganizations.Add("G20");
                        if (summary.Contains("ASEAN")) data.InternationalOrganizations.Add("ASEAN");
                    }
                }
            }
            catch { }
        }

        private CountryData MapToCountryData(JsonElement el)
        {
            var nameEl = el.GetProperty("name");
            var currencies = new Dictionary<string, (string Name, string Symbol)>();
            if (el.TryGetProperty("currencies", out var currEl) && currEl.ValueKind == JsonValueKind.Object)
            {
                foreach (var prop in currEl.EnumerateObject())
                {
                    var cName = prop.Value.TryGetProperty("name", out var cn) ? cn.GetString() ?? "" : "";
                    var cSym = prop.Value.TryGetProperty("symbol", out var cs) ? cs.GetString() ?? "" : "";
                    currencies[prop.Name] = (cName, cSym);
                }
            }

            var languages = new List<string>();
            if (el.TryGetProperty("languages", out var langEl) && langEl.ValueKind == JsonValueKind.Object)
            {
                foreach (var prop in langEl.EnumerateObject())
                {
                    languages.Add(prop.Value.GetString() ?? "");
                }
            }

            var latlng = el.TryGetProperty("latlng", out var ll) && ll.GetArrayLength() >= 2 
                ? (ll[0].GetDouble(), ll[1].GetDouble()) 
                : (0.0, 0.0);

            return new CountryData
            {
                Name = nameEl.TryGetProperty("common", out var common) ? common.GetString() ?? "" : "",
                OfficialName = nameEl.TryGetProperty("official", out var official) ? official.GetString() ?? "" : "",
                FlagUrl = el.TryGetProperty("flags", out var fl) && fl.TryGetProperty("svg", out var svg) ? svg.GetString() ?? "" : "",
                FlagEmoji = el.TryGetProperty("flag", out var fe) ? fe.GetString() ?? "" : "",
                IsoCode = el.TryGetProperty("cca2", out var code) ? code.GetString() ?? "" : "",
                Capital = el.TryGetProperty("capital", out var cap) && cap.GetArrayLength() > 0 ? cap[0].GetString() ?? "" : "N/A",
                Region = el.TryGetProperty("region", out var reg) ? reg.GetString() ?? "" : "",
                Subregion = el.TryGetProperty("subregion", out var sub) ? sub.GetString() ?? "" : "",
                Continents = el.TryGetProperty("continents", out var cont) ? cont.EnumerateArray().Select(c => c.GetString() ?? "").ToList() : new List<string>(),
                Population = el.TryGetProperty("population", out var pop) ? pop.GetInt64() : 0,
                Area = el.TryGetProperty("area", out var area) ? area.GetDouble() : 0,
                Languages = languages,
                Currencies = currencies,
                Timezones = el.TryGetProperty("timezones", out var tz) ? tz.EnumerateArray().Select(t => t.GetString() ?? "").ToList() : new List<string>(),
                Borders = el.TryGetProperty("borders", out var bord) ? bord.EnumerateArray().Select(b => b.GetString() ?? "").ToList() : new List<string>(),
                GoogleMapsUrl = el.TryGetProperty("maps", out var maps) && maps.TryGetProperty("googleMaps", out var gm) ? gm.GetString() ?? "" : "",
                Latitude = latlng.Item1,
                Longitude = latlng.Item2,
                DrivingSide = el.TryGetProperty("car", out var car) && car.TryGetProperty("side", out var side) ? side.GetString() ?? "N/A" : "N/A",
                InternetDomains = el.TryGetProperty("tld", out var tld) ? tld.EnumerateArray().Select(d => d.GetString() ?? "").ToList() : new List<string>(),
                CallingCode = el.TryGetProperty("idd", out var idd) 
                    ? (idd.TryGetProperty("root", out var root) ? root.GetString() ?? "" : "") + 
                      (idd.TryGetProperty("suffixes", out var suff) && suff.GetArrayLength() > 0 ? suff[0].GetString() ?? "" : "")
                    : "N/A"
            };
        }
    }
}
