using System;
using System.ComponentModel.DataAnnotations;

namespace TodoListApp.Models
{
    public class TodoItem
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        [StringLength(100, ErrorMessage = "Title cannot be longer than 100 characters.")]
        public string Title { get; set; } = string.Empty;

        public bool IsCompleted { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public DateTime? DueDate { get; set; }

        [Required]
        public string Priority { get; set; } = "Medium"; // Low, Medium, High
        
        public string UserId { get; set; } = string.Empty;
    }
}
