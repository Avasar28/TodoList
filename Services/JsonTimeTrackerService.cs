using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using TodoListApp.Models;

namespace TodoListApp.Services
{
    public class JsonTimeTrackerService : ITimeTrackerService
    {
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly string _filePath;

        public JsonTimeTrackerService(IWebHostEnvironment webHostEnvironment)
        {
            _webHostEnvironment = webHostEnvironment;
            _filePath = Path.Combine(_webHostEnvironment.ContentRootPath, "timetracker.json"); // Save in root like todos.json
        }

        private List<TimeTrackerEntry> LoadEntries()
        {
            if (!File.Exists(_filePath))
            {
                return new List<TimeTrackerEntry>();
            }

            var json = File.ReadAllText(_filePath);
            if (string.IsNullOrWhiteSpace(json)) return new List<TimeTrackerEntry>();

            try
            {
                return JsonSerializer.Deserialize<List<TimeTrackerEntry>>(json) ?? new List<TimeTrackerEntry>();
            }
            catch
            {
                return new List<TimeTrackerEntry>();
            }
        }

        private void SaveEntries(List<TimeTrackerEntry> entries)
        {
            var json = JsonSerializer.Serialize(entries, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_filePath, json);
        }

        public IEnumerable<TimeTrackerEntry> GetEntries(int userId, DateTime date)
        {
            var allEntries = LoadEntries();
            return allEntries.Where(e => e.UserId == userId && e.Date.Date == date.Date);
        }

        public void AddEntry(TimeTrackerEntry entry)
        {
            var entries = LoadEntries();
            entry.Id = Guid.NewGuid(); // Ensure ID is set
            entries.Add(entry);
            SaveEntries(entries);
        }

        public void UpdateEntry(TimeTrackerEntry entry)
        {
            var entries = LoadEntries();
            var existing = entries.FirstOrDefault(e => e.Id == entry.Id && e.UserId == entry.UserId);
            if (existing != null)
            {
                existing.Description = entry.Description;
                existing.StartTime = entry.StartTime;
                existing.EndTime = entry.EndTime;
                existing.Date = entry.Date;
                SaveEntries(entries);
            }
        }

        public void DeleteEntry(Guid id, int userId)
        {
            var entries = LoadEntries();
            var entry = entries.FirstOrDefault(e => e.Id == id && e.UserId == userId);
            if (entry != null)
            {
                entries.Remove(entry);
                SaveEntries(entries);
            }
        }
    }
}
