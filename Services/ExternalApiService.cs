using System;
using System.Net.Http;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace TodoListApp.Services
{
    public class ExternalApiService : IExternalApiService
    {
        private readonly HttpClient _httpClient;

        public ExternalApiService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

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

                var lat = results[0].GetProperty("latitude").GetDouble();
                var lon = results[0].GetProperty("longitude").GetDouble();
                
                var name = results[0].GetProperty("name").GetString() ?? "";
                var district = results[0].TryGetProperty("admin2", out var d) ? d.GetString() : "";
                var state = results[0].TryGetProperty("admin1", out var s) ? s.GetString() : "";
                var country = results[0].TryGetProperty("country", out var c) ? c.GetString() : "";

                var data = await FetchWeatherDataAsync(lat, lon, name);
                data.LocalArea = name;
                data.District = district ?? "";
                data.State = state ?? "";
                data.Country = country ?? "";
                
                // If name is a PIN/Number, use district/state as City
                if (int.TryParse(name, out _)) {
                    data.City = !string.IsNullOrEmpty(district) ? district : (!string.IsNullOrEmpty(state) ? state : name);
                } else {
                    data.City = name;
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
                    cityName = root.TryGetProperty("city", out var c) ? c.GetString() ?? "My Location" : (localArea ?? "My Location");
                    state = root.TryGetProperty("principalSubdivision", out var s) ? s.GetString() ?? "" : "";
                    country = root.TryGetProperty("countryName", out var cn) ? cn.GetString() ?? "" : "";
                }
            }
            catch { }

            try
            {
                var data = await FetchWeatherDataAsync(lat, lon, cityName ?? localArea ?? "My Location");
                data.LocalArea = localArea ?? "";
                data.State = state ?? "";
                data.Country = country ?? "";
                
                // If locality and city differ, locality is likely the village/area
                if (!string.IsNullOrEmpty(localArea) && localArea != cityName) {
                    data.City = localArea;
                    data.District = cityName ?? "My Location";
                } else {
                    data.City = cityName ?? "My Location";
                }
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
                var url = $"https://api.frankfurter.app/latest?amount=1&from={from}&to={to}";
                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();
                var json = await response.Content.ReadAsStringAsync();

                using var doc = JsonDocument.Parse(json);
                var rates = doc.RootElement.GetProperty("rates");
                var rate = rates.GetProperty(to).GetDecimal();

                return new CurrencyConversionData
                {
                    From = from,
                    To = to,
                    Rate = rate,
                    ConvertedAmount = rate,
                    LastUpdated = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss") + " UTC"
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
                return new UserLocationData { City = "London", TimeZoneId = "GMT Standard Time", Currency = "USD" };

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

        public static string GetCurrencyFlag(string currencyCode) => currencyCode.ToUpper() switch {
            "USD" => "🇺🇸", "EUR" => "🇪🇺", "GBP" => "🇬🇧", "JPY" => "🇯🇵", "AUD" => "🇦🇺",
            "CAD" => "🇨🇦", "CHF" => "🇨🇭", "CNY" => "🇨🇳", "HKD" => "🇭🇰", "NZD" => "🇳🇿",
            "INR" => "🇮🇳", "BRL" => "🇧🇷", "RUB" => "🇷🇺", "KRW" => "🇰🇷", "MXN" => "🇲🇽",
            "SGD" => "🇸🇬", "THB" => "🇹🇭", "TRY" => "🇹🇷", "ZAR" => "🇿🇦", "ILS" => "🇮🇱",
            "PHP" => "🇵🇭", "MYR" => "🇲🇾", "IDR" => "🇮🇩", "CZK" => "🇨🇿", "HUF" => "🇭🇺",
            "PLN" => "🇵🇱", "RON" => "🇷🇴", "SEK" => "🇸🇪", "ISK" => "🇮🇸", "NOK" => "🇳🇴",
            "HRK" => "🇭🇷", "BGN" => "🇧🇬", "DKK" => "🇩🇰", "AED" => "🇦🇪", "SAR" => "🇸🇦", _ => "🏳️"
        };
    }
}
