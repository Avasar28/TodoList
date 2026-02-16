using System;

namespace TodoListApp.Models
{
    public class ActivityLog
    {
        public int Id { get; set; }
        public string Action { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty; // Target user
        public string PerformedBy { get; set; } = string.Empty; // Executing user
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public string Details { get; set; } = string.Empty;
    }
}
