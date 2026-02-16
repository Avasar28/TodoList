using System;
using System.ComponentModel.DataAnnotations;

namespace TodoListApp.Models
{
    public class UserAchievement
    {
        public int Id { get; set; }
        public string UserId { get; set; }
        public string AchievementName { get; set; }
        public string Description { get; set; }
        public string Icon { get; set; }
        public DateTime AchievedAt { get; set; }
    }
}
