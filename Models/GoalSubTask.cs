using System;
using System.ComponentModel.DataAnnotations;

namespace TodoListApp.Models
{
    public class GoalSubTask
    {
        [Key]
        public int Id { get; set; }

        public int GoalId { get; set; }
        public Goal? Goal { get; set; }

        [Required]
        [StringLength(200)]
        public string Title { get; set; } = string.Empty;

        public bool IsCompleted { get; set; } = false;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
