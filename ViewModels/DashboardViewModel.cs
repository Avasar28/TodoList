using System.Collections.Generic;
using TodoListApp.Models;
using TodoListApp.Services;

namespace TodoListApp.ViewModels
{
    public class TimeZoneOption
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Offset { get; set; } = string.Empty;
        public string Abbr { get; set; } = string.Empty; // e.g. IST, EST
        public string FullName { get; set; } = string.Empty; // Includes cities/countries search terms
    }

    public class NewsItem
    {
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Source { get; set; } = string.Empty;
        public string Published { get; set; } = string.Empty;
        public string Link { get; set; } = string.Empty;
        public string ImageUrl { get; set; } = string.Empty;
        public string SourceUrl { get; set; } = string.Empty;
        public string Sentiment { get; set; } = "Neutral"; // Positive, Neutral, Negative
        public string LocationTag { get; set; } = string.Empty;
        public string Category { get; set; } = "General";
    }

    public class NewsData
    {
        public List<NewsItem> Items { get; set; } = new List<NewsItem>();
        public string ResolvedLocation { get; set; } = string.Empty;
    }

    public class DashboardViewModel
    {
        public string UserName { get; set; } = string.Empty;
        public UserPreferences Preferences { get; set; } = new UserPreferences();

        // Real-time features
        public WeatherData Weather { get; set; } = new WeatherData();
        public CurrencyConversionData Currency { get; set; } = new CurrencyConversionData();
        public TimeData TimeConversion { get; set; } = new TimeData();
        public NewsData News { get; set; } = new NewsData();

        // Interactive Inputs
        public string SelectedCity { get; set; } = "";
        public string FromCurrency { get; set; } = "USD";
        public string ToCurrency { get; set; } = "EUR";
        public string SourceTimeZone { get; set; } = ""; // Start empty
        public string TargetTimeZone { get; set; } = ""; // Start empty

        public static List<string> StaticAvailableCurrencies { get; set; } = new List<string> { 
            "USD", "EUR", "GBP", "JPY", "INR", "AUD", "CAD", "CHF", "CNY", "HKD", 
            "NZD", "BRL", "RUB", "KRW", "MXN", "SGD", "THB", "TRY", "ZAR", "ILS", 
            "PHP", "MYR", "IDR", "CZK", "HUF", "PLN", "RON", "SEK", "ISK", "NOK", 
            "HRK", "BGN", "DKK", "AED", "SAR" 
        };

        public List<string> AvailableCurrencies { get; set; } = new List<string>(StaticAvailableCurrencies);
        public List<TimeZoneOption> AvailableTimeZones { get; set; } = new List<TimeZoneOption>();
    }
}
