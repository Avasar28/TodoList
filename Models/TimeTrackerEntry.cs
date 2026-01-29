using System;

namespace TodoListApp.Models
{
    public class TimeTrackerEntry
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public int UserId { get; set; }
        public string Description { get; set; } = string.Empty;
        
        // Stored as string "HH:mm" for simplicity in this context, or TimeSpan. 
        // Let's use DateTime or TimeSpan for better calculation, but JSON serialization might be easier depending on format.
        // Requirement said "Time Picker" which usually gives "HH:mm".
        // Let's store as DateTime to handle date crossing if needed, but requirements imply "Daily" tracker.
        // Storing as strings "HH:mm" is often easiest for simple daily trackers, but calculations need parsing.
        // Let's use DateTime for robustness, but we only care about the Time part for the view usually?
        // Actually, if we use DateTime, we can calculate duration easily.
        
        public DateTime Date { get; set; } // The date this entry belongs to
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }

        // Computed property for duration
        public TimeSpan Duration => EndTime - StartTime;
    }
}
