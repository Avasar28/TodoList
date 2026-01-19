using System.Threading.Tasks;
using System.Collections.Generic;

namespace TodoListApp.Services
{
    public interface IExternalApiService
    {
        Task<WeatherData> GetWeatherAsync(string city);
        Task<WeatherData> GetWeatherByCoordsAsync(double lat, double lon);
        Task<CurrencyConversionData> GetCurrencyRateAsync(string fromCurrency, string toCurrency);
        Task<TimeData> GetTimeConversionAsync(string sourceTimeZone, string targetTimeZone, string? customTime = null);
        Task<UserLocationData> GetLocationFromIpAsync(string ip);
        Task<CurrencyHistoryData> GetCurrencyHistoryAsync(string from, string to, int days = 7);
        Task<string?> GetCurrencyFromLocationAsync(string location);
    }

    public class WeatherData
    {
        // 1. Location Details
        public string City { get; set; } = string.Empty;
        public string District { get; set; } = string.Empty;
        public string LocalArea { get; set; } = string.Empty;
        public string State { get; set; } = string.Empty;
        public string Country { get; set; } = string.Empty;
        public double Latitude { get; set; }
        public double Longitude { get; set; }

        // 2. Current Weather
        public double Temperature { get; set; }
        public string Description { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;
        public double FeelsLike { get; set; }
        public int Humidity { get; set; }
        public double WindSpeed { get; set; }
        public int WindDirection { get; set; }
        public double Visibility { get; set; } // km
        public double Pressure { get; set; } // hPa
        public double DewPoint { get; set; }

        // 3. Forecast
        public List<DailyForecast> Forecasts { get; set; } = new List<DailyForecast>();

        // 4. Air Quality & UV
        public int AQI { get; set; }
        public string AQILevel { get; set; } = string.Empty;
        public double UVIndex { get; set; }
        public string UVRisk { get; set; } = string.Empty;

        // 5. Sun & Moon
        public string Sunrise { get; set; } = string.Empty;
        public string Sunset { get; set; } = string.Empty;
        public string Moonrise { get; set; } = string.Empty;
        public string Moonset { get; set; } = string.Empty;
        public string MoonPhase { get; set; } = string.Empty;
        public string MoonIcon { get; set; } = string.Empty;
    }

    public class DailyForecast
    {
        public string Date { get; set; } = string.Empty;
        public double MaxTemp { get; set; }
        public double MinTemp { get; set; }
        public string Icon { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }

    public class CurrencyConversionData
    {
        public string From { get; set; } = string.Empty;
        public string To { get; set; } = string.Empty;
        public decimal Rate { get; set; }
        public decimal ConvertedAmount { get; set; } // For 1 unit
        public string LastUpdated { get; set; } = string.Empty;
        public bool? IsUp { get; set; } // null if unknown, true if up, false if down
    }

    public class TimeData
    {
        public string SourceZone { get; set; } = string.Empty;
        public string TargetZone { get; set; } = string.Empty;
        public string SourceTime { get; set; } = string.Empty;
        public string TargetTime { get; set; } = string.Empty;
        public double TotalOffsetMinutes { get; set; }
        public string DayChange { get; set; } = string.Empty; // "Next Day", "Previous Day", "Same Day"
        public string OffsetDisplay { get; set; } = string.Empty; // "+09:30"
        public string SourceAbbr { get; set; } = string.Empty; // "IST"
        public string TargetAbbr { get; set; } = string.Empty; // "GMT"
    }

    public class UserLocationData
    {
        public string City { get; set; } = string.Empty;
        public string TimeZoneId { get; set; } = string.Empty;
        public string Currency { get; set; } = string.Empty;
    }

    public class CurrencyHistoryData
    {
        public List<string> Labels { get; set; } = new List<string>(); // Dates
        public List<decimal> Values { get; set; } = new List<decimal>(); // Rates
    }
}
