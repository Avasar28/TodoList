using System;
using System.Net.Http;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.Extensions.Configuration;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using TodoListApp.ViewModels;

namespace TodoListApp.Services
{
    public class ExternalApiService : IExternalApiService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _config;
        private static List<NewsItem> _newsCache = new List<NewsItem>();
        private static JsonDocument? _tempCache;

        public ExternalApiService(HttpClient httpClient, IConfiguration config)
        {
            _httpClient = httpClient;
            _config = config;
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
                data.CountryCode = result.TryGetProperty("country_code", out var cc) ? cc.GetString() ?? "" : "";
                
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
            string countryCode = "";
            
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
                    countryCode = root.TryGetProperty("countryCode", out var cc) ? cc.GetString() ?? "" : "";

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
                data.CountryCode = countryCode ?? "";
                
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
                string resolvedName = location;
                string countryCode = "US";

                try 
                {
                    var geoUrl = $"https://geocoding-api.open-meteo.com/v1/search?name={Uri.EscapeDataString(location)}&count=1&language=en&format=json";
                    // Short timeout for geocoding as it's optional
                    using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(3));
                    var geoRes = await _httpClient.GetAsync(geoUrl, cts.Token);
                    
                    if (geoRes.IsSuccessStatusCode)
                    {
                        var geoJson = await geoRes.Content.ReadAsStringAsync();
                        using var doc = JsonDocument.Parse(geoJson);
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
                }
                catch 
                {
                    // If geocoding fails (DNS, Timeout, etc.), just use the original location string.
                    // This ensures the news search still proceeds even if we can't pretty-print the location name.
                    Console.WriteLine($"[News] Geocoding failed for '{location}', falling back to raw query.");
                }

                // 2. Build Query
                var queryBuilder = new System.Text.StringBuilder($"{location}"); 
                
                if (!string.IsNullOrEmpty(category) && category != "All")
                {
                    queryBuilder.Append($" {category}");
                }
                else
                {
                    // If no category, ensure we get News (Google search works better with this hint)
                    if (!location.ToLower().Contains("news")) queryBuilder.Append(" news");
                }

                var finalQuery = Uri.EscapeDataString(queryBuilder.ToString());
                
                // --- GOOGLE NEWS RSS ENGINE ---
                var googleUrl = $"https://news.google.com/rss/search?q={finalQuery}&hl=en-{countryCode}&gl={countryCode}&ceid={countryCode}:en";
                var googleXml = await _httpClient.GetStringAsync(googleUrl);
               
                // --- PARSE GOOGLE RSS (Fast & Lightweight) ---
                var gDoc = XDocument.Parse(googleXml);
                var items = gDoc.Descendants("item").Select(item => {
                    var title = item.Element("title")?.Value ?? "";
                    var description = item.Element("description")?.Value ?? "";
                    
                    var sourceEl = item.Element("source");
                    var source = sourceEl?.Value ?? "News";
                    var sourceUrl = sourceEl?.Attribute("url")?.Value ?? "";
                    
                    var pubDate = item.Element("pubDate")?.Value ?? "";
                    var link = item.Element("link")?.Value ?? "";
                    
                    // Basic cleanup
                    var cleanSummary = Regex.Replace(description, "<.*?>", "");
                    cleanSummary = System.Net.WebUtility.HtmlDecode(cleanSummary).Trim();
                    if (cleanSummary.Length > 300) cleanSummary = cleanSummary.Substring(0, 297) + "...";

                    if (DateTime.TryParse(pubDate, out var dt)) pubDate = dt.ToString("MMM dd, yyyy HH:mm");
                    if (title.Contains(" - ")) title = title.Substring(0, title.LastIndexOf(" - ")).Trim();

                    // --- INSIGHT ENGINE ---
                    var topic = GetNewsCategory(title, description, resolvedName);
                    var sentiment = AnalyzeSentiment(title, description);
                    var locationTag = ExtractLocationTag(title, description, resolvedName);

                    return new NewsItem {
                        Title = title,
                        Description = cleanSummary,
                        Source = source,
                        SourceUrl = sourceUrl,
                        Published = pubDate,
                        Link = link,
                        Sentiment = sentiment,
                        LocationTag = locationTag,
                        Category = topic 
                    };
                }).Where(x => !string.IsNullOrEmpty(x.Title)).ToList();

                // Sort by Latest if requested
                if (sortBy == "latest")
                {
                    items = items.OrderByDescending(i => {
                        DateTime.TryParse(i.Published, out var dt);
                        return dt;
                    }).ToList();
                }

                _newsCache = items; 

                return new NewsData {
                    Items = items,
                    ResolvedLocation = resolvedName
                };

            }
            catch (Exception ex)
            {
                Console.WriteLine($"News Error: {ex.Message}");
                // Return a single error item so the UI isn't empty
                var errorItem = new NewsItem {
                    Title = "Service Unavailable",
                    Description = "We are unable to connect to the news service at this time. Please check your internet connection or try again later.",
                    Source = "System",
                    SourceUrl = "#",
                    Published = DateTime.Now.ToString("MMM dd, yyyy HH:mm"),
                    Link = "#",
                    Sentiment = "Neutral",
                    LocationTag = "System",
                    Category = "Error"
                };
                return new NewsData { ResolvedLocation = location, Items = new List<NewsItem> { errorItem } };
            }
        }

        private string AnalyzeSentiment(string title, string desc)
        {
            var text = (title + " " + desc).ToLower();
            
            // Positive Keywords
            var posMatch = Regex.Matches(text, @"\b(growth|surge|gain|profit|win|success|breakthrough|positive|better|recovery|helping|innovation|excellent|bright|jump|soar|top|best|solution|cure|peace|deal|signed|launch|expanding)\b").Count;
            
            // Negative Keywords
            var negMatch = Regex.Matches(text, @"\b(drop|crash|loss|fail|error|warning|risk|danger|crisis|war|strike|down|lowest|bad|issue|protest|death|killed|accident|storm|damage|fear|inflation|bankrupt|threat)\b").Count;

            if (posMatch > negMatch) return "Positive";
            if (negMatch > posMatch) return "Negative";
            return "Neutral";
        }

        private string ExtractLocationTag(string title, string desc, string queryLocation)
        {
            var text = (title + " " + desc).ToLower();
            
            // Priority 1: User's searched location
            if (text.Contains(queryLocation.ToLower())) return queryLocation;

            // Priority 2: Common Country/Region matches
            if (text.Contains("uk") || text.Contains("britain") || text.Contains("london")) return "UK";
            if (text.Contains("us") || text.Contains("america") || text.Contains("washington") || text.Contains("new york")) return "USA";
            if (text.Contains("india") || text.Contains("delhi") || text.Contains("mumbai")) return "India";
            if (text.Contains("china") || text.Contains("beijing")) return "China";
            if (text.Contains("europe") || text.Contains("eu")) return "Europe";
            if (text.Contains("canada")) return "Canada";
            if (text.Contains("australia")) return "Australia";

            // Priority 3: Try to find Capitalized cities (simplified) - if we had a list, we'd use it, but let's keep it simple
            return "Global";
        }

        // --- SMART TOPIC ANALYSIS (Refined NLP-Lite) ---
        private string GetNewsCategory(string title, string desc, string locationName)
        {
            var text = (title + " " + desc).ToLower();
            
            // 0. Local / Location Based (Priority)
            // If the news explicitly mentions the searched city (e.g. "Surat")
            if (!string.IsNullOrEmpty(locationName) && text.Contains(locationName.ToLower())) return "City";

            // 1. Tech & Science
            if (Regex.IsMatch(text, @"\b(ai|artificial intelligence|apple|google|microsoft|meta|nvidia|crypto|bitcoin|blockchain|software|cyber|hacker|nasa|space|galaxy|rocket|quantum|robot|smartphone|app|update|feature|device|tech)\b")) return "Technology";
            
            // 2. Business & Economy
            if (Regex.IsMatch(text, @"\b(stock|market|economy|inflation|gdp|bank|invest|trade|revenue|profit|ceo|startup|business|finance|fed|rates|tax|gold|oil|price|deal|merger|housing|real estate|property)\b")) return "Business";
            
            // 3. Politics & World
            if (Regex.IsMatch(text, @"\b(election|poll|vote|president|minister|parliament|senate|law|court|policy|war|army|military|nato|un|treaty|diplomacy|protest|strike|democrat|republican|government|prime minister|biden|trump|modi)\b")) return "Politics";
            
            // 4. Sports
            if (Regex.IsMatch(text, @"\b(sport|football|soccer|cricket|tennis|nba|nfl|f1|formula 1|league|cup|champion|player|team|coach|score|medal|olympic|game|match|win|loss|tournament|ipl)\b")) return "Sports";
            
            // 5. Health
            if (Regex.IsMatch(text, @"\b(health|doctor|hospital|virus|vaccine|cancer|medicine|study|research|diet|nutrition|sleep|menatal|fitness|disease|covid|flu|pandemic)\b")) return "Health";

            // 6. Entertainment
            if (Regex.IsMatch(text, @"\b(movie|film|actor|music|song|concert|festival|award|oscar|grammy|netflix|star|celebrity|hollywood|series|show|artist|cinema)\b")) return "Entertainment";

            // 7. Environment & Weather
            if (Regex.IsMatch(text, @"\b(climate|weather|rain|storm|hurricane|flood|heat|global warming|carbon|forest|wildfire|earth|ocean|solar|energy|temperature|monsoon|snow)\b")) return "Environment";

            return "General";
        }

        // --- DYNAMIC VISUAL MAPPER (Curated Pools) ---
        // Returns a varied, high-quality image from a pool based on the article hash.
        private string GetDynamicImage(string category, string articleTitle)
        {
            // Deterministic Index: Abs(Hash) % Count
            // Ensures the same article always gets the same image, but different articles get different ones.
            int hash = Math.Abs(articleTitle.GetHashCode());
            
            string[] pool = category switch
            {
                "Technology" => new[] {
                    "https://images.unsplash.com/photo-1518770660439-4636190af475?w=800&q=80", // Circuit
                    "https://images.unsplash.com/photo-1550751827-4bd374c3f58b?w=800&q=80", // Cyber
                    "https://images.unsplash.com/photo-1488590528505-98d2b5aba04b?w=800&q=80", // Code
                    "https://images.unsplash.com/photo-1531297461136-8208630f9604?w=800&q=80", // Chip
                    "https://images.unsplash.com/photo-1526374965328-7f61d4dc18c5?w=800&q=80"  // Matrix
                },
                "Business" => new[] {
                    "https://images.unsplash.com/photo-1460925895917-afdab827c52f?w=800&q=80", // Chart
                    "https://images.unsplash.com/photo-1590283603385-17ffb3a7f29f?w=800&q=80", // Stocks
                    "https://images.unsplash.com/photo-1486406146926-c627a92ad1ab?w=800&q=80", // Skyscrappers
                    "https://images.unsplash.com/photo-1554224155-984063681ee4?w=800&q=80", // Meeting
                    "https://images.unsplash.com/photo-1611974765270-ca12586343bb?w=800&q=80"  // Trading
                },
                "Politics" => new[] {
                    "https://images.unsplash.com/photo-1541872703-74c59636a226?w=800&q=80", // Podium
                    "https://images.unsplash.com/photo-1529101091760-61df6be5d18b?w=800&q=80", // Flags
                    "https://images.unsplash.com/photo-1555848962-6e79363ec58f?w=800&q=80", // Vote
                    "https://images.unsplash.com/photo-1477281746055-612b48937664?w=800&q=80", // Capitol
                    "https://images.unsplash.com/photo-1575320181282-9afab399332c?w=800&q=80"  // Microphone
                },
                "Sports" => new[] {
                    "https://images.unsplash.com/photo-1461896836934-ffe607ba8211?w=800&q=80", // Runner
                    "https://images.unsplash.com/photo-1579952363873-27f3bde9be2b?w=800&q=80", // Ball
                    "https://images.unsplash.com/photo-1471295253337-3ceaaedca402?w=800&q=80", // Stadium
                    "https://images.unsplash.com/photo-1517466787929-bc90951d0974?w=800&q=80", // Gym
                    "https://images.unsplash.com/photo-1526620810753-226d9422f934?w=800&q=80"  // Soccer
                },
                "Health" => new[] {
                    "https://images.unsplash.com/photo-1505751172876-fa1923c5c528?w=800&q=80", // Steth
                    "https://images.unsplash.com/photo-1532938911079-1b06ac7ceec7?w=800&q=80", // Medicine
                    "https://images.unsplash.com/photo-1571019614242-c5c5dee9f50b?w=800&q=80", // Workout
                    "https://images.unsplash.com/photo-1584036561566-b9370001e9e3?w=800&q=80", // Lab
                    "https://images.unsplash.com/photo-1527613426441-4da17471b66d?w=800&q=80"  // Nurse
                },
                "Entertainment" => new[] {
                    "https://images.unsplash.com/photo-1499364615650-ec38552f4f34?w=800&q=80", // Stage
                    "https://images.unsplash.com/photo-1598899134739-24c46f58b8c0?w=800&q=80", // Cinema
                    "https://images.unsplash.com/photo-1514525253440-b393452e8d26?w=800&q=80", // Concert
                    "https://images.unsplash.com/photo-1511671782779-c97d3d27a1d4?w=800&q=80", // Mic
                    "https://images.unsplash.com/photo-1603190287605-e6ade32fa852?w=800&q=80"  // Popcorn
                },
                "Environment" => new[] {
                    "https://images.unsplash.com/photo-1470071459604-3b5ec3a7fe05?w=800&q=80", // Nature
                    "https://images.unsplash.com/photo-1534274988757-9d7d6a36eb82?w=800&q=80", // Sun
                    "https://images.unsplash.com/photo-1428908728789-d2de25dbd4e2?w=800&q=80", // Clouds
                    "https://images.unsplash.com/photo-1520121401995-928cd50d4e27?w=800&q=80", // Lightning
                    "https://images.unsplash.com/photo-1441974231531-c6227db76b6e?w=800&q=80"  // Forest
                },
                "City" => new[] {
                    "https://images.unsplash.com/photo-1449824913935-59a10b8d2000?w=800&q=80", // City Generic
                    "https://images.unsplash.com/photo-1480714378408-67cf0d13bc1b?w=800&q=80", // NY Vibe
                    "https://images.unsplash.com/photo-1519501025264-65ba15a82390?w=800&q=80", // Urban
                    "https://images.unsplash.com/photo-1477959858617-67f85cf4f1df?w=800&q=80", // Skyline
                    "https://images.unsplash.com/photo-1496568816309-51d7c20e3b21?w=800&q=80"  // Night City
                },
                "Error" => new[] {
                    "https://images.unsplash.com/photo-1594322436404-5a0526db4d13?w=800&q=80" // Storm/Warning visual
                },
                _ => new[] {
                    "https://images.unsplash.com/photo-1504711434969-e33886168f5c?w=800&q=80", // News ppr
                    "https://images.unsplash.com/photo-1585829365295-ab7cd400c167?w=800&q=80", // Newspaper 2
                    "https://images.unsplash.com/photo-1590523277543-a94d2e4eb00b?w=800&q=80"  // Read
                }
            };
            
            return pool[hash % pool.Length];
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
                // Normalize query for better matching
                var normalizedQuery = query.Trim().ToLower();
                
                // Search by name, but also try alternate spellings and codes if query is short
                string searchUrl = $"https://restcountries.com/v3.1/name/{Uri.EscapeDataString(normalizedQuery)}?fields=name,flags,cca2,capital,region,subregion,continents,population,area,languages,currencies,timezones,borders,maps,flag";
                
                var response = await _httpClient.GetAsync(searchUrl);
                
                // If name search fails and query is 2-3 chars, try alpha code
                if (!response.IsSuccessStatusCode && normalizedQuery.Length >= 2 && normalizedQuery.Length <= 3)
                {
                    searchUrl = $"https://restcountries.com/v3.1/alpha/{Uri.EscapeDataString(normalizedQuery)}?fields=name,flags,cca2,capital,region,subregion,continents,population,area,languages,currencies,timezones,borders,maps,flag";
                    response = await _httpClient.GetAsync(searchUrl);
                }

                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(json);
                    
                    if (doc.RootElement.ValueKind == JsonValueKind.Array)
                    {
                        return doc.RootElement.EnumerateArray()
                            .Select(MapToCountryData)
                            .OrderByDescending(c => c.Name.Equals(normalizedQuery, StringComparison.OrdinalIgnoreCase) || c.OfficialName.Equals(normalizedQuery, StringComparison.OrdinalIgnoreCase))
                            .ToList();
                    }
                    else if (doc.RootElement.ValueKind == JsonValueKind.Object)
                    {
                        // Single result from alpha search
                        return new List<CountryData> { MapToCountryData(doc.RootElement) };
                    }
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
                // Try full text search first (safest for exact matches)
                var response = await _httpClient.GetAsync($"https://restcountries.com/v3.1/name/{Uri.EscapeDataString(name)}?fullText=true&fields=name,flags,cca2,capital,region,subregion,continents,population,area,languages,currencies,timezones,borders,maps,flag,latlng,idd,tld,car");
                
                // If fail, try partial name search (normalization fallback)
                if (!response.IsSuccessStatusCode)
                {
                    response = await _httpClient.GetAsync($"https://restcountries.com/v3.1/name/{Uri.EscapeDataString(name)}?fields=name,flags,cca2,capital,region,subregion,continents,population,area,languages,currencies,timezones,borders,maps,flag,latlng,idd,tld,car");
                }

                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(json);
                    var results = doc.RootElement.ValueKind == JsonValueKind.Array ? doc.RootElement.EnumerateArray().ToList() : new List<JsonElement> { doc.RootElement };
                    
                    if (results.Any())
                    {
                        // Prioritize the best match
                        var bestMatch = results.FirstOrDefault(r => 
                            r.GetProperty("name").GetProperty("common").GetString()?.Equals(name, StringComparison.OrdinalIgnoreCase) == true ||
                            r.GetProperty("name").GetProperty("official").GetString()?.Equals(name, StringComparison.OrdinalIgnoreCase) == true);
                        
                        if (bestMatch.ValueKind == JsonValueKind.Undefined || bestMatch.ValueKind == JsonValueKind.Null)
                        {
                            bestMatch = results[0];
                        }

                        var data = MapToCountryData(bestMatch);
                        
                        // Concurrent Enrichment
                        var tasks = new List<Task>
                        {
                            EnrichWithWikipedia(data),
                            EnrichWithGDP(data),
                            EnrichWithClimate(data),
                            EnrichWithBorderNames(data),
                            Task.Run(() => EnrichWithElectricity(data))
                        };
                        
                        await Task.WhenAll(tasks);
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

        private async Task EnrichWithBorderNames(CountryData data)
        {
            if (data.Borders == null || !data.Borders.Any())
            {
                data.BorderCountryNames = new List<string>();
                return;
            }

            try
            {
                // Fetch border country names concurrently
                var borderNameTasks = data.Borders.Select(async isoCode =>
                {
                    try
                    {
                        var response = await _httpClient.GetAsync($"https://restcountries.com/v3.1/alpha/{isoCode}?fields=name");
                        if (response.IsSuccessStatusCode)
                        {
                            var json = await response.Content.ReadAsStringAsync();
                            using var doc = JsonDocument.Parse(json);
                            var nameObj = doc.RootElement.GetProperty("name");
                            return nameObj.TryGetProperty("common", out var common) ? common.GetString() ?? isoCode : isoCode;
                        }
                    }
                    catch { }
                    return isoCode; // Fallback to ISO code if fetch fails
                }).ToList();

                var borderNames = await Task.WhenAll(borderNameTasks);
                data.BorderCountryNames = borderNames.ToList();
            }
            catch
            {
                // If enrichment fails entirely, fallback to ISO codes
                data.BorderCountryNames = data.Borders.ToList();
            }
        }

        private async Task EnrichWithWikipedia(CountryData data)
        {
            try
            {
                var wikiResponse = await _httpClient.GetAsync($"https://en.wikipedia.org/api/rest_v1/page/summary/{Uri.EscapeDataString(data.Name)}");
                if (!wikiResponse.IsSuccessStatusCode)
                {
                    // Try official name if common name fails
                    wikiResponse = await _httpClient.GetAsync($"https://en.wikipedia.org/api/rest_v1/page/summary/{Uri.EscapeDataString(data.OfficialName)}");
                }

                if (wikiResponse.IsSuccessStatusCode)
                {
                    var wikiJson = await wikiResponse.Content.ReadAsStringAsync();
                    using var wikiDoc = JsonDocument.Parse(wikiJson);
                    var root = wikiDoc.RootElement;

                    if (root.TryGetProperty("extract", out var extract))
                    {
                        string summary = extract.GetString() ?? "";
                        data.WikipediaSummary = summary;
                        
                        // Extract National Motto (Improved Regex)
                        var mottoMatch = Regex.Match(summary, "is [\"']?([^\"'.]+)[\"']?\\s+is the national motto", RegexOptions.IgnoreCase) 
                                     ?? Regex.Match(summary, "motto is [\"']?([^\"'.]+)[\"']?", RegexOptions.IgnoreCase);
                        if (mottoMatch.Success) data.NationalMotto = mottoMatch.Groups[1].Value.Trim('"', '\'');

                        // Extract Religions (heuristic)
                        if (summary.Contains("Muslim", StringComparison.OrdinalIgnoreCase)) data.Religions = "Islam (Predominant)";
                        else if (summary.Contains("Christian", StringComparison.OrdinalIgnoreCase)) data.Religions = "Christianity (Predominant)";
                        else if (summary.Contains("Hindu", StringComparison.OrdinalIgnoreCase)) data.Religions = "Hinduism (Predominant)";
                        else if (summary.Contains("Buddhist", StringComparison.OrdinalIgnoreCase)) data.Religions = "Buddhism (Predominant)";
                        
                        // Extract Industry mentions
                        var industries = new List<string>();
                        if (summary.Contains("agriculture", StringComparison.OrdinalIgnoreCase)) industries.Add("Agriculture");
                        if (summary.Contains("technology", StringComparison.OrdinalIgnoreCase)) industries.Add("Technology");
                        if (summary.Contains("manufacturing", StringComparison.OrdinalIgnoreCase)) industries.Add("Manufacturing");
                        if (summary.Contains("tourism", StringComparison.OrdinalIgnoreCase)) industries.Add("Tourism");
                        if (summary.Contains("oil", StringComparison.OrdinalIgnoreCase) || summary.Contains("petroleum", StringComparison.OrdinalIgnoreCase)) industries.Add("Petroleum");
                        if (industries.Any()) data.MajorIndustries = string.Join(", ", industries);

                        // Extract Government Type
                        if (summary.Contains("constitutional monarchy", StringComparison.OrdinalIgnoreCase)) 
                            data.GovernmentType = "Constitutional Monarchy";
                        else if (summary.Contains("parliamentary republic", StringComparison.OrdinalIgnoreCase)) 
                            data.GovernmentType = "Parliamentary Republic";
                        else if (summary.Contains("federal republic", StringComparison.OrdinalIgnoreCase)) 
                            data.GovernmentType = "Federal Republic";
                        else if (summary.Contains("presidential republic", StringComparison.OrdinalIgnoreCase)) 
                            data.GovernmentType = "Presidential Republic";
                        else if (summary.Contains("semi-presidential", StringComparison.OrdinalIgnoreCase)) 
                            data.GovernmentType = "Semi-Presidential Republic";
                        else if (summary.Contains("absolute monarchy", StringComparison.OrdinalIgnoreCase)) 
                            data.GovernmentType = "Absolute Monarchy";
                        else if (summary.Contains("monarchy", StringComparison.OrdinalIgnoreCase)) 
                            data.GovernmentType = "Monarchy";
                        else if (summary.Contains("republic", StringComparison.OrdinalIgnoreCase)) 
                            data.GovernmentType = "Republic";
                        else if (summary.Contains("theocracy", StringComparison.OrdinalIgnoreCase)) 
                            data.GovernmentType = "Theocracy";
                        else if (summary.Contains("dictatorship", StringComparison.OrdinalIgnoreCase)) 
                            data.GovernmentType = "Dictatorship";

                        // Extract Independence Date
                        var indepPatterns = new[]
                        {
                            @"independence (?:on|in) (\d{1,2}\s+\w+\s+\d{4})",
                            @"independent (?:on|since|in) (\d{1,2}\s+\w+\s+\d{4}|\d{4})",
                            @"gained independence (?:on|in) (\d{1,2}\s+\w+\s+\d{4}|\d{4})",
                            @"became independent (?:on|in) (\d{1,2}\s+\w+\s+\d{4}|\d{4})"
                        };
                        
                        foreach (var pattern in indepPatterns)
                        {
                            var indepMatch = Regex.Match(summary, pattern, RegexOptions.IgnoreCase);
                            if (indepMatch.Success)
                            {
                                data.IndependenceDay = indepMatch.Groups[1].Value;
                                break;
                            }
                        }

                        // Climate Type heuristic
                        if (summary.Contains("tropical", StringComparison.OrdinalIgnoreCase)) data.ClimateType = "Tropical";
                        else if (summary.Contains("temperate", StringComparison.OrdinalIgnoreCase)) data.ClimateType = "Temperate";
                        else if (summary.Contains("desert", StringComparison.OrdinalIgnoreCase) || summary.Contains("arid", StringComparison.OrdinalIgnoreCase)) data.ClimateType = "Arid / Desert";
                        else if (summary.Contains("mediterranean", StringComparison.OrdinalIgnoreCase)) data.ClimateType = "Mediterranean";
                        else if (summary.Contains("continental", StringComparison.OrdinalIgnoreCase)) data.ClimateType = "Continental";
                        else if (summary.Contains("polar", StringComparison.OrdinalIgnoreCase) || summary.Contains("arctic", StringComparison.OrdinalIgnoreCase)) data.ClimateType = "Polar / Arctic";
                        else if (summary.Contains("subarctic", StringComparison.OrdinalIgnoreCase)) data.ClimateType = "Subarctic";

                        // Natural Resources heuristic
                        var resources = new List<string>();
                        var resourceKeywords = new[] { "gold", "oil", "gas", "timber", "minerals", "coal", "iron", "copper", "diamonds" };
                        foreach (var kw in resourceKeywords) if (summary.Contains(kw, StringComparison.OrdinalIgnoreCase)) resources.Add(char.ToUpper(kw[0]) + kw.Substring(1));
                        if (resources.Any()) data.NaturalResources = string.Join(", ", resources);

                        // Organizations
                        var orgs = new List<string>();
                        if (summary.Contains("United Nations") || summary.Contains("UN ")) orgs.Add("UN");
                        if (summary.Contains("World Health Organization") || summary.Contains("WHO ")) orgs.Add("WHO");
                        if (summary.Contains("European Union") || summary.Contains("EU ")) orgs.Add("EU");
                        if (summary.Contains("NATO")) orgs.Add("NATO");
                        if (summary.Contains("G20")) orgs.Add("G20");
                        if (summary.Contains("WTO")) orgs.Add("WTO");
                        if (summary.Contains("Commonwealth")) orgs.Add("Commonwealth");
                        data.InternationalOrganizations = orgs.Any() ? orgs : new List<string> { "Member of UN and various international bodies" };

                        // Extract National Anthem
                        var anthemMatch = Regex.Match(summary, @"national anthem is [""']?([^""'.]+)[""']?", RegexOptions.IgnoreCase);
                        if (anthemMatch.Success) data.NationalAnthem = anthemMatch.Groups[1].Value;

                        // Extract National Animal
                        var animalPatterns = new[]
                        {
                            @"national animal is (?:the )?([^.,]+)",
                            @"national bird is (?:the )?([^.,]+)",
                            @"national symbol is (?:the )?([^.,]+)"
                        };
                        foreach (var pattern in animalPatterns)
                        {
                            var match = Regex.Match(summary, pattern, RegexOptions.IgnoreCase);
                            if (match.Success)
                            {
                                data.NationalAnimal = match.Groups[1].Value.Trim();
                                break;
                            }
                        }

                        // Extract National Sport
                        var sportMatch = Regex.Match(summary, @"national sport is ([^.,]+)", RegexOptions.IgnoreCase);
                        if (sportMatch.Success) data.NationalSport = sportMatch.Groups[1].Value.Trim();

                        // Global Rankings
                        if (summary.Contains("wealthiest", StringComparison.OrdinalIgnoreCase)) data.GlobalRankings["Economic Status"] = "Top Tier";
                        if (summary.Contains("highest human development", StringComparison.OrdinalIgnoreCase)) data.GlobalRankings["Human Development"] = "Very High";
                        if (summary.Contains("UNESCO World Heritage", StringComparison.OrdinalIgnoreCase)) data.GlobalRankings["UNESCO Heritage"] = "Multiple Sites";
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

            // Extract borders
            var borders = el.TryGetProperty("borders", out var bord) ? bord.EnumerateArray().Select(b => b.GetString() ?? "").ToList() : new List<string>();
            
            // Determine if landlocked (has borders but no coastline, or explicitly marked as landlocked)
            bool isLandlocked = el.TryGetProperty("landlocked", out var ll_flag) && ll_flag.GetBoolean();

            return new CountryData
            {
                Name = nameEl.TryGetProperty("common", out var common) ? common.GetString() ?? "Unknown Name" : "Unknown Name",
                OfficialName = nameEl.TryGetProperty("official", out var official) ? official.GetString() ?? "Not officially defined" : "Not officially defined",
                FlagUrl = el.TryGetProperty("flags", out var fl) && fl.TryGetProperty("svg", out var svg) ? svg.GetString() ?? "" : "",
                FlagEmoji = el.TryGetProperty("flag", out var fe) ? fe.GetString() ?? "" : "",
                IsoCode = el.TryGetProperty("cca2", out var code) ? code.GetString() ?? "" : "",
                IsoCodeAlpha3 = el.TryGetProperty("cca3", out var code3) ? code3.GetString() ?? "" : "",
                Capital = el.TryGetProperty("capital", out var cap) && cap.GetArrayLength() > 0 ? cap[0].GetString() ?? "Not officially defined" : "Not officially defined",
                Region = el.TryGetProperty("region", out var reg) ? reg.GetString() ?? "Not officially defined" : "Not officially defined",
                Subregion = el.TryGetProperty("subregion", out var sub) ? sub.GetString() ?? "Not officially defined" : "Not officially defined",
                Continents = el.TryGetProperty("continents", out var cont) ? cont.EnumerateArray().Select(c => c.GetString() ?? "").ToList() : new List<string>(),
                Population = el.TryGetProperty("population", out var pop) ? pop.GetInt64() : 0,
                Area = el.TryGetProperty("area", out var area) ? area.GetDouble() : 0,
                Languages = languages.Any() ? languages : new List<string> { "Data not publicly available" },
                Currencies = currencies,
                Timezones = el.TryGetProperty("timezones", out var tz) ? tz.EnumerateArray().Select(t => t.GetString() ?? "").ToList() : new List<string>(),
                Borders = borders,
                IsLandlocked = isLandlocked,
                GoogleMapsUrl = el.TryGetProperty("maps", out var maps) && maps.TryGetProperty("googleMaps", out var gm) ? gm.GetString() ?? "" : "",
                Latitude = latlng.Item1,
                Longitude = latlng.Item2,
                DrivingSide = el.TryGetProperty("car", out var car) && car.TryGetProperty("side", out var side) ? side.GetString() ?? "Not officially defined" : "Not officially defined",
                InternetDomains = el.TryGetProperty("tld", out var tld) ? tld.EnumerateArray().Select(d => d.GetString() ?? "").ToList() : new List<string>(),
                CallingCode = el.TryGetProperty("idd", out var idd) 
                    ? (idd.TryGetProperty("root", out var root) ? root.GetString() ?? "" : "") + 
                      (idd.TryGetProperty("suffixes", out var suff) && suff.GetArrayLength() > 0 ? suff[0].GetString() ?? "" : "")
                    : "Not officially defined",
                Demonym = el.TryGetProperty("demonyms", out var dem) && 
                          dem.TryGetProperty("eng", out var engDem) && 
                          engDem.TryGetProperty("m", out var male) 
                    ? male.GetString() ?? "Not officially defined" 
                    : "Not officially defined"
            };
        }
        public async Task<List<ViewModels.HolidayCountry>> GetAvailableCountriesAsync()
        {
            try
            {
                // Fetch all countries from RestCountries to ensure a comprehensive list
                var response = await _httpClient.GetAsync("https://restcountries.com/v3.1/all?fields=name,cca2,flag");
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(json);
                    var countries = new List<ViewModels.HolidayCountry>();

                    if (doc.RootElement.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var el in doc.RootElement.EnumerateArray())
                        {
                            var nameObj = el.GetProperty("name");
                            countries.Add(new ViewModels.HolidayCountry
                            {
                                CountryCode = el.GetProperty("cca2").GetString() ?? "",
                                Name = nameObj.GetProperty("common").GetString() ?? "",
                                FlagEmoji = el.GetProperty("flag").GetString() ?? ""
                            });
                        }
                    }
                    return countries.OrderBy(c => c.Name).ToList();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error fetching comprehensive country list: {ex.Message}");
            }

            // Fallback to Nager.Date if RestCountries fails (though it has fewer countries)
            try
            {
                var response = await _httpClient.GetAsync("https://date.nager.at/api/v3/AvailableCountries");
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    return JsonSerializer.Deserialize<List<ViewModels.HolidayCountry>>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new List<ViewModels.HolidayCountry>();
                }
            }
            catch { }

            return new List<ViewModels.HolidayCountry>();
        }

        public async Task<ViewModels.HolidayData> GetPublicHolidaysAsync(string countryCode, int year)
        {
            var data = new ViewModels.HolidayData { CountryCode = countryCode, Year = year };
            var apiKey = _config["HolidaySettings:ApiKey"];

            try
            {
                // 1. Try Calendarific if API Key is provided (Covers 230+ countries)
                if (!string.IsNullOrWhiteSpace(apiKey))
                {
                    var calendarificHolidays = await FetchFromCalendarificAsync(countryCode, year, apiKey);
                    if (calendarificHolidays.Any())
                    {
                        data.Holidays = calendarificHolidays;
                        data.TotalCount = calendarificHolidays.Count;
                        return data;
                    }
                }

                // 2. Try Nager.Date (Free, covers ~100 countries)
                var nagerHolidays = await FetchFromNagerDateAsync(countryCode, year);
                if (nagerHolidays.Any())
                {
                    data.Holidays = nagerHolidays;
                    data.TotalCount = nagerHolidays.Count;
                    return data;
                }

                // 3. Fallback to Google Calendar Public ICS (Zero-key, broad coverage)
                var googleHolidays = await FetchFromGoogleCalendarAsync(countryCode, year);
                data.Holidays = googleHolidays;
                data.TotalCount = googleHolidays.Count;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing holidays for {countryCode}: {ex.Message}");
            }
            return data;
        }

        private async Task<List<ViewModels.HolidayItem>> FetchFromGoogleCalendarAsync(string countryCode, int year)
        {
            var holidays = new List<ViewModels.HolidayItem>();
            try
            {
                // Mapping some common exceptions for Google Calendar IDs
                var googleId = countryCode.ToUpper() switch
                {
                    "IN" => "indian",
                    "US" => "usa",
                    "GB" => "uk",
                    "AU" => "australian",
                    "CA" => "canadian",
                    "DE" => "german",
                    "FR" => "french",
                    "IT" => "italian",
                    "ES" => "spain",
                    "JP" => "japanese",
                    "CN" => "china",
                    "RU" => "russian",
                    "BR" => "brazilian",
                    "MX" => "mexican",
                    _ => countryCode.ToLower() // Most others Use iso2
                };

                var url = $"https://calendar.google.com/calendar/ical/en.{googleId}%23holiday%40group.v.calendar.google.com/public/basic.ics";
                var response = await _httpClient.GetAsync(url);
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    
                    // Simple ICS Parsing (Extracting SUMMARY and DTSTART)
                    var events = content.Split(new[] { "BEGIN:VEVENT" }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var ev in events)
                    {
                        if (!ev.Contains("END:VEVENT")) continue;

                        string summary = string.Empty;
                        string dateStr = string.Empty;

                        var lines = ev.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
                        foreach (var line in lines)
                        {
                            if (line.StartsWith("SUMMARY:")) 
                                summary = line.Substring(8).Replace("\\,", ",");
                            else if (line.StartsWith("DTSTART;VALUE=DATE:")) 
                                dateStr = line.Substring(19).Trim();
                            else if (line.StartsWith("DTSTART:")) 
                                dateStr = line.Substring(8).Trim();
                        }

                        if (!string.IsNullOrEmpty(summary) && !string.IsNullOrEmpty(dateStr) && dateStr.StartsWith(year.ToString()))
                        {
                            // Parse date from YYYYMMDD
                            if (dateStr.Length >= 8)
                            {
                                var formattedDate = $"{dateStr.Substring(0, 4)}-{dateStr.Substring(4, 2)}-{dateStr.Substring(6, 2)}";
                                if (DateTime.TryParse(formattedDate, out var dt))
                                {
                                    holidays.Add(new ViewModels.HolidayItem
                                    {
                                        Name = summary,
                                        LocalName = summary,
                                        Date = formattedDate,
                                        DayOfWeek = dt.DayOfWeek.ToString(),
                                        Types = summary.ToLower().Contains("observance") ? new List<string> { "Observance" } : new List<string> { "Public" }
                                    });
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Google Calendar Fetch Error: {ex.Message}");
            }
            return holidays.OrderBy(h => h.Date).ToList();
        }

        private async Task<List<ViewModels.HolidayItem>> FetchFromCalendarificAsync(string countryCode, int year, string apiKey)
        {
            var holidays = new List<ViewModels.HolidayItem>();
            try
            {
                var url = $"https://calendarific.com/api/v2/holidays?&api_key={apiKey}&country={countryCode}&year={year}";
                var response = await _httpClient.GetAsync(url);
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(content);
                    if (doc.RootElement.TryGetProperty("response", out var resp) && resp.TryGetProperty("holidays", out var hlist) && hlist.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var el in hlist.EnumerateArray())
                        {
                            var hDate = el.GetProperty("date").GetProperty("iso").GetString() ?? "";
                            var hItem = new ViewModels.HolidayItem
                            {
                                Name = el.GetProperty("name").GetString() ?? "",
                                LocalName = el.GetProperty("name").GetString() ?? "",
                                Date = hDate.Contains("T") ? hDate.Split('T')[0] : hDate,
                                Types = new List<string>()
                            };

                            if (el.TryGetProperty("type", out var tArray) && tArray.ValueKind == JsonValueKind.Array)
                            {
                                hItem.Types = tArray.EnumerateArray().Select(t => t.GetString() ?? "").ToList();
                            }

                            if (DateTime.TryParse(hItem.Date, out var dt))
                            {
                                hItem.DayOfWeek = dt.DayOfWeek.ToString();
                            }
                            holidays.Add(hItem);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Calendarific API Error: {ex.Message}");
            }
            return holidays;
        }

        private async Task<List<ViewModels.HolidayItem>> FetchFromNagerDateAsync(string countryCode, int year)
        {
            var holidays = new List<ViewModels.HolidayItem>();
            try
            {
                var response = await _httpClient.GetAsync($"https://date.nager.at/api/v3/PublicHolidays/{year}/{countryCode}");
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    if (string.IsNullOrWhiteSpace(content)) return holidays;

                    var trimmed = content.TrimStart();
                    if (!trimmed.StartsWith("[")) return holidays;

                    holidays = JsonSerializer.Deserialize<List<ViewModels.HolidayItem>>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new List<ViewModels.HolidayItem>();
                    
                    foreach (var h in holidays)
                    {
                        if (DateTime.TryParse(h.Date, out var dt))
                        {
                            h.DayOfWeek = dt.DayOfWeek.ToString();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Nager.Date API Error: {ex.Message}");
            }
            return holidays;
        }

        private string GetImageByKeywords(string text)
        {
            text = text.ToLowerInvariant();
            
            // Map keywords to reliable Unsplash Images (Direct links)
            if (text.Contains("traffic") || text.Contains("transport") || text.Contains("bus") || text.Contains("train") || text.Contains("driver"))
                return "https://images.unsplash.com/photo-1449965408869-eaa3f722e40d?auto=format&fit=crop&w=800&q=80"; // City Traffic
                
            if (text.Contains("market") || text.Contains("economy") || text.Contains("business") || text.Contains("finance") || text.Contains("stock") || text.Contains("money") || text.Contains("price"))
                return "https://images.unsplash.com/photo-1486406146926-c627a92ad1ab?auto=format&fit=crop&w=800&q=80"; // Business/Skyscrapers
                
            if (text.Contains("house") || text.Contains("home") || text.Contains("property") || text.Contains("estate") || text.Contains("rent") || text.Contains("housing"))
                return "https://images.unsplash.com/photo-1560518883-ce09059eeffa?auto=format&fit=crop&w=800&q=80"; // Modern House
                
            if (text.Contains("police") || text.Contains("crime") || text.Contains("arrest") || text.Contains("security") || text.Contains("court") || text.Contains("law"))
                return "https://images.unsplash.com/photo-1453873531674-2151bcd01707?auto=format&fit=crop&w=800&q=80"; // City Night/Police vibe (Abstract)
                
            if (text.Contains("tech") || text.Contains("data") || text.Contains("digital") || text.Contains("cyber") || text.Contains("ai") || text.Contains("online"))
                return "https://images.unsplash.com/photo-1518770660439-4636190af475?auto=format&fit=crop&w=800&q=80"; // Technology/Chip
                
            if (text.Contains("weather") || text.Contains("storm") || text.Contains("rain") || text.Contains("flood") || text.Contains("sun") || text.Contains("climate"))
                return "https://images.unsplash.com/photo-1561470508-fd4df1ed90b2?auto=format&fit=crop&w=800&q=80"; // Weather/Cloudy

            if (text.Contains("london") || text.Contains("uk") || text.Contains("britain"))
                return "https://images.unsplash.com/photo-1513635269975-59663e0ac1ad?auto=format&fit=crop&w=800&q=80"; // London Big Ben

             // Default catch-all (Newspaper/Reading) - Different from the main placeholder to add variety
            return "https://images.unsplash.com/photo-1504711434969-e33886168f5c?auto=format&fit=crop&w=800&q=80";
        }
    }
}
