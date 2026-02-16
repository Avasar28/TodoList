using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TodoListApp.Models
{
    public class GoalSchedule
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int GoalId { get; set; }

        [ForeignKey("GoalId")]
        public virtual Goal? Goal { get; set; }

        [Required]
        public DateTime ScheduledDate { get; set; }

        [Required]
        public string StartTime { get; set; } = "09:00";

        [Required]
        public string EndTime { get; set; } = "10:00";

        [StringLength(500)]
        public string? Notes { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
