using System.Collections.Generic;
using TodoListApp.Models;
using TodoListApp.Services;

namespace TodoListApp.ViewModels
{
    public class TimeZoneOption
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
    }

    public class DashboardViewModel
    {
        public string UserName { get; set; } = string.Empty;
        public UserPreferences Preferences { get; set; } = new UserPreferences();

        // Real-time features
        public WeatherData Weather { get; set; } = new WeatherData();
        public CurrencyConversionData Currency { get; set; } = new CurrencyConversionData();
        public TimeData TimeConversion { get; set; } = new TimeData();

        // Interactive Inputs
        public string SelectedCity { get; set; } = "London";
        public string FromCurrency { get; set; } = "USD";
        public string ToCurrency { get; set; } = "EUR";
        public string SourceTimeZone { get; set; } = "UTC";
        public string TargetTimeZone { get; set; } = "GMT Standard Time";

        public List<string> AvailableCurrencies { get; set; } = new List<string> { "USD", "EUR", "GBP", "JPY", "INR", "AUD", "CAD", "CHF" };
        public List<TimeZoneOption> AvailableTimeZones { get; set; } = new List<TimeZoneOption>();
    }
}
