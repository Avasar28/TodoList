using System.Threading.Tasks;
using System.Collections.Generic;

namespace TodoListApp.Services
{
    public interface IExternalApiService
    {
        Task<WeatherData> GetWeatherAsync(string city);
        Task<CurrencyConversionData> GetCurrencyRateAsync(string fromCurrency, string toCurrency);
        Task<TimeData> GetTimeConversionAsync(string sourceTimeZone, string targetTimeZone);
        Task<UserLocationData> GetLocationFromIpAsync(string ip);
        Task<CurrencyHistoryData> GetCurrencyHistoryAsync(string from, string to, int days = 7);
    }

    public class WeatherData
    {
        public string City { get; set; } = string.Empty;
        public double Temperature { get; set; }
        public string Description { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;
        public List<DailyForecast> Forecasts { get; set; } = new List<DailyForecast>();
    }

    public class DailyForecast
    {
        public string Date { get; set; } = string.Empty; // e.g., "Mon", "Tue"
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
    }

    public class TimeData
    {
        public string SourceZone { get; set; } = string.Empty;
        public string TargetZone { get; set; } = string.Empty;
        public string SourceTime { get; set; } = string.Empty;
        public string TargetTime { get; set; } = string.Empty;
        public double TotalOffsetMinutes { get; set; }
    }

    public class UserLocationData
    {
        public string City { get; set; } = string.Empty;
        public string TimeZoneId { get; set; } = string.Empty;
    }

    public class CurrencyHistoryData
    {
        public List<string> Labels { get; set; } = new List<string>(); // Dates
        public List<decimal> Values { get; set; } = new List<decimal>(); // Rates
    }
}
