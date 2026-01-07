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
                // 1. Geocoding
                var geoUrl = $"https://geocoding-api.open-meteo.com/v1/search?name={city}&count=1&language=en&format=json";
                var geoResponse = await _httpClient.GetAsync(geoUrl);
                geoResponse.EnsureSuccessStatusCode();
                var geoJson = await geoResponse.Content.ReadAsStringAsync();
                
                using var geoDoc = JsonDocument.Parse(geoJson);
                var results = geoDoc.RootElement.GetProperty("results");
                if (results.GetArrayLength() == 0) return new WeatherData { City = city, Description = "City not found" };

                var lat = results[0].GetProperty("latitude").GetDouble();
                var lon = results[0].GetProperty("longitude").GetDouble();
                var cityName = results[0].GetProperty("name").GetString();

                // 2. Weather
                var weatherUrl = $"https://api.open-meteo.com/v1/forecast?latitude={lat}&longitude={lon}&current=temperature_2m,weather_code&daily=temperature_2m_max,temperature_2m_min,weather_code&timezone=auto";
                var weatherResponse = await _httpClient.GetAsync(weatherUrl);
                weatherResponse.EnsureSuccessStatusCode();
                var weatherJson = await weatherResponse.Content.ReadAsStringAsync();

                using var weatherDoc = JsonDocument.Parse(weatherJson);
                var current = weatherDoc.RootElement.GetProperty("current");
                var temp = current.GetProperty("temperature_2m").GetDouble();
                var code = current.GetProperty("weather_code").GetInt32();

                var weatherData = new WeatherData
                {
                    City = cityName,
                    Temperature = temp,
                    Description = GetWeatherDescription(code),
                    Icon = GetWeatherIcon(code)
                };

                // Parse Daily Forecast
                if (weatherDoc.RootElement.TryGetProperty("daily", out var daily))
                {
                   var dates = daily.GetProperty("time").EnumerateArray().ToList();
                   var maxTemps = daily.GetProperty("temperature_2m_max").EnumerateArray().ToList();
                   var minTemps = daily.GetProperty("temperature_2m_min").EnumerateArray().ToList();
                   var codes = daily.GetProperty("weather_code").EnumerateArray().ToList();

                   for (int i = 1; i < dates.Count && i <= 5; i++) // Start from index 1 (tomorrow), take 5 days
                   {
                       var dateStr = dates[i].GetString();
                       if (DateTime.TryParse(dateStr, out var date))
                       {
                           weatherData.Forecasts.Add(new DailyForecast
                           {
                               Date = date.ToString("ddd"), // Mon, Tue
                               MaxTemp = maxTemps[i].GetDouble(),
                               MinTemp = minTemps[i].GetDouble(),
                               Description = GetWeatherDescription(codes[i].GetInt32()),
                               Icon = GetWeatherIcon(codes[i].GetInt32())
                           });
                       }
                   }
                }

                return weatherData;
            }
            catch
            {
                return new WeatherData { City = city, Description = "Service unavailable", Temperature = 0 };
            }
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
                    ConvertedAmount = rate // Since amount is 1
                };
            }
            catch
            {
                return new CurrencyConversionData { From = from, To = to, Rate = 0, ConvertedAmount = 0 };
            }
        }

        public Task<TimeData> GetTimeConversionAsync(string sourceZoneId, string targetZoneId)
        {
            var now = DateTime.UtcNow;
            
            TimeZoneInfo sourceZone = GetTimeZone(sourceZoneId);
            TimeZoneInfo targetZone = GetTimeZone(targetZoneId);

            var sourceTime = TimeZoneInfo.ConvertTimeFromUtc(now, sourceZone);
            var targetTime = TimeZoneInfo.ConvertTimeFromUtc(now, targetZone);

            return Task.FromResult(new TimeData
            {
                SourceZone = sourceZone.DisplayName, // Or StandardName
                TargetZone = targetZone.DisplayName,
                SourceTime = sourceTime.ToString("t"),
                TargetTime = targetTime.ToString("t"), // Short time string
                TotalOffsetMinutes = targetZone.GetUtcOffset(now).TotalMinutes
            });
        }

        public async Task<UserLocationData> GetLocationFromIpAsync(string ip)
        {
            if (string.IsNullOrEmpty(ip) || ip == "::1" || ip == "127.0.0.1")
            {
                return new UserLocationData { City = "London", TimeZoneId = "GMT Standard Time" };
            }

            try
            {
                var response = await _httpClient.GetAsync($"http://ip-api.com/json/{ip}");
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.GetProperty("status").GetString() == "success")
                    {
                        var city = doc.RootElement.GetProperty("city").GetString() ?? "London";
                        var timezone = doc.RootElement.GetProperty("timezone").GetString(); // e.g., "Asia/Kolkata"
                         // Map IANA timezone to Windows ID if needed, or rely on .NET Core's cross-platform support
                        return new UserLocationData { City = city, TimeZoneId = timezone ?? "UTC" };
                    }
                }
            }
            catch 
            {
                // Fallback
            }
            return new UserLocationData { City = "London", TimeZoneId = "GMT Standard Time" };
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
                    data.Labels.Add(day.Name); // Date string
                    if (day.Value.TryGetProperty(to, out var rateElement))
                    {
                        data.Values.Add(rateElement.GetDecimal());
                    }
                }
                return data;
            }
            catch
            {
                return new CurrencyHistoryData();
            }
        }

        private TimeZoneInfo GetTimeZone(string id)
        {
            try
            {
                // Try finding by ID
                return TimeZoneInfo.FindSystemTimeZoneById(id);
            }
            catch
            {
                // Fallback attempt for common aliases or just UTC
                 if (id.Contains("Pacific")) return TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time");
                 if (id.Contains("Eastern")) return TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
                 if (id.Contains("GMT") || id.Contains("London")) return TimeZoneInfo.FindSystemTimeZoneById("GMT Standard Time");
                 if (id.Contains("Tokyo")) return TimeZoneInfo.FindSystemTimeZoneById("Tokyo Standard Time");
                 if (id.Contains("India")) return TimeZoneInfo.FindSystemTimeZoneById("India Standard Time");
                 
                return TimeZoneInfo.Utc;
            }
        }

        private string GetWeatherDescription(int code)
        {
            return code switch
            {
                0 => "Clear sky",
                1 or 2 or 3 => "Partly cloudy",
                45 or 48 => "Fog",
                51 or 53 or 55 => "Drizzle",
                61 or 63 or 65 => "Rain",
                71 or 73 or 75 => "Snow",
                95 or 96 or 99 => "Thunderstorm",
                _ => "Unknown"
            };
        }

        private string GetWeatherIcon(int code)
        {
            return code switch
            {
                0 => "☀️",
                1 or 2 or 3 => "⛅",
                45 or 48 => "🌫️",
                51 or 53 or 55 => "🌦️",
                61 or 63 or 65 => "🌧️",
                71 or 73 or 75 => "❄️",
                95 or 96 or 99 => "⚡",
                _ => "🌡️"
            };
        }
    }
}
