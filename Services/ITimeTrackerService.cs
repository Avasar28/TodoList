using System;
using System.Collections.Generic;
using TodoListApp.Models;

namespace TodoListApp.Services
{
    public interface ITimeTrackerService
    {
        IEnumerable<TimeTrackerEntry> GetEntries(string userId, DateTime date);
        IEnumerable<TimeTrackerEntry> GetEntriesRange(string userId, DateTime startDate, DateTime endDate);
        void AddEntry(TimeTrackerEntry entry);
        void UpdateEntry(TimeTrackerEntry entry);
        void DeleteEntry(Guid id, string userId);
    }
}
