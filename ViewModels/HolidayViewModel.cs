using System;
using System.Collections.Generic;

namespace TodoListApp.ViewModels
{
    public class HolidayItem
    {
        public string Name { get; set; } = string.Empty;
        public string LocalName { get; set; } = string.Empty;
        public string Date { get; set; } = string.Empty;
        public string DayOfWeek { get; set; } = string.Empty;
        public List<string> Types { get; set; } = new List<string>();
        public bool Global { get; set; }
        public List<string>? Counties { get; set; }
        public int? LaunchYear { get; set; }
    }

    public class HolidayData
    {
        public List<HolidayItem> Holidays { get; set; } = new List<HolidayItem>();
        public int TotalCount { get; set; }
        public string CountryCode { get; set; } = string.Empty;
        public int Year { get; set; }
    }

    public class HolidayCountry
    {
        public string CountryCode { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string FlagEmoji { get; set; } = string.Empty;
    }
}
