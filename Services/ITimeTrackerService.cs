using System;
using System.Collections.Generic;
using TodoListApp.Models;

namespace TodoListApp.Services
{
    public interface ITimeTrackerService
    {
        IEnumerable<TimeTrackerEntry> GetEntries(int userId, DateTime date);
        void AddEntry(TimeTrackerEntry entry);
        void UpdateEntry(TimeTrackerEntry entry);
        void DeleteEntry(Guid id, int userId);
    }
}
