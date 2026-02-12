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
        Task<string?> GetTimeZoneByLocationAsync(string location);
        Task<ViewModels.NewsData> GetNewsAsync(string location, string? category = null, string sortBy = "relevance");
        Task<string> GetNewsDetailAsync(string url);
        List<ViewModels.NewsItem> GetCachedNews();
        Task<List<CountryData>> SearchCountriesAsync(string query);
        Task<CountryData?> GetCountryDetailsAsync(string name);
        Task<List<ViewModels.HolidayCountry>> GetAvailableCountriesAsync();
        Task<ViewModels.HolidayData> GetPublicHolidaysAsync(string countryCode, int year);
        Task<List<EmergencyCountryData>> GetAllEmergencyNumbersAsync();
    }

    public class EmergencyCountryData
    {
        public string Country { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public string Flag { get; set; } = string.Empty;
        public List<EmergencyService> Services { get; set; } = new List<EmergencyService>();
    }

    public class EmergencyService
    {
        public string Name { get; set; } = string.Empty;
        public string Number { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
    }

    public class WeatherData
    {
        // 1. Location Details
        public string City { get; set; } = string.Empty;
        public string District { get; set; } = string.Empty;
        public string LocalArea { get; set; } = string.Empty;
        public string State { get; set; } = string.Empty;
        public string Country { get; set; } = string.Empty;
        public string CountryCode { get; set; } = string.Empty; // ISO-2
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

    public class CountryData
    {
        public string Name { get; set; } = string.Empty;
        public string OfficialName { get; set; } = string.Empty;
        public string FlagUrl { get; set; } = string.Empty;
        public string FlagEmoji { get; set; } = string.Empty;
        public string IsoCode { get; set; } = string.Empty; // Alpha-2
        public string IsoCodeAlpha3 { get; set; } = string.Empty; // Alpha-3
        public string Capital { get; set; } = string.Empty;
        public string Region { get; set; } = string.Empty;
        public string Subregion { get; set; } = string.Empty;
        public List<string> Continents { get; set; } = new List<string>();
        public long Population { get; set; }
        public double Area { get; set; }
        public double PopulationDensity => Area > 0 ? Population / Area : 0;
        public List<string> Languages { get; set; } = new List<string>();
        public Dictionary<string, (string Name, string Symbol)> Currencies { get; set; } = new Dictionary<string, (string Name, string Symbol)>();
        public List<string> Timezones { get; set; } = new List<string>();
        public List<string> Borders { get; set; } = new List<string>(); // ISO codes
        public List<string> BorderCountryNames { get; set; } = new List<string>(); // Full country names
        public bool IsLandlocked { get; set; }
        public string GoogleMapsUrl { get; set; } = string.Empty;
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        
        // Advanced Fields (from Wikipedia/CIA)
        public string GovernmentType { get; set; } = "Not officially defined";
        public string HeadOfState { get; set; } = "Data not publicly available";
        public string IndependenceDay { get; set; } = "Not officially defined";
        public string NationalMotto { get; set; } = "Not officially defined";
        public string MajorEthnicGroups { get; set; } = "Data not publicly available";
        public string WikipediaSummary { get; set; } = "Global summary is currently being retrieved...";
        public string LandArea { get; set; } = "Data not publicly available";
        public string WaterArea { get; set; } = "Data not publicly available";

        // Economy
        public string GDP { get; set; } = "Data not publicly available";
        public string MajorIndustries { get; set; } = "Data not publicly available";

        // Culture & Society
        public string NationalAnthem { get; set; } = "Not officially defined";
        public string NationalAnimal { get; set; } = "Not officially defined";
        public string NationalSport { get; set; } = "Not officially defined";
        public string Religions { get; set; } = "Data not publicly available";
        public string Demonym { get; set; } = "Not officially defined"; // Citizen name

        // Infrastructure
        public string DrivingSide { get; set; } = "Not officially defined";
        public List<string> InternetDomains { get; set; } = new List<string>();
        public string CallingCode { get; set; } = "Not officially defined";
        public string ElectricityVoltage { get; set; } = "Not officially defined";
        public string ElectricityPlugTypes { get; set; } = "Not officially defined";

        // Climate & Environment
        public string ClimateType { get; set; } = "Data not publicly available";
        public string AverageTemperature { get; set; } = "Data not publicly available";
        public string NaturalResources { get; set; } = "Data not publicly available";

        // Additional Information
        public List<string> FamousPlaces { get; set; } = new List<string>();
        public Dictionary<string, string> GlobalRankings { get; set; } = new Dictionary<string, string>();
        public List<string> InternationalOrganizations { get; set; } = new List<string>();
    }
}
