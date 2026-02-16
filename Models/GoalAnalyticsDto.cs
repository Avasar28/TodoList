using System;
using System.Collections.Generic;

namespace TodoListApp.Models
{
    public class GoalAnalyticsDto
    {
        public int TotalGoals { get; set; }
        public int CompletedGoals { get; set; }
        public int ActiveGoals { get; set; }
        public int OverdueGoals { get; set; }
        public double CompletionRate { get; set; }
        public Dictionary<string, int> MonthlyCompletionData { get; set; } = new Dictionary<string, int>();
        public Dictionary<string, int> CategoryBreakdown { get; set; } = new Dictionary<string, int>();
        
        // Productivity Intelligence
        public int GoalsAtRisk { get; set; }
        public int StagnantGoals { get; set; }
        public List<string> SmartInsights { get; set; } = new List<string>();
    }
}
