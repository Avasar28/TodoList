using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TodoListApp.Models
{
    public class Goal
    {
        [Key]
        public int Id { get; set; }

        public string UserId { get; set; } = string.Empty;

        [Required]
        [StringLength(200)]
        public string Title { get; set; } = string.Empty;

        [StringLength(1000)]
        public string? Description { get; set; }

        [StringLength(50)]
        public string? Category { get; set; } // e.g., Health, Work, Learning

        [Column(TypeName = "decimal(18,2)")]
        public decimal TargetValue { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal CurrentValue { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal ProgressPercent { get; set; } // auto calculated

        [StringLength(50)]
        public string? Unit { get; set; } // e.g., hrs, pages, tasks

        public DateTime StartDate { get; set; }

        public DateTime EndDate { get; set; }

        [StringLength(50)]
        public string Status { get; set; } = "Active"; // Active, On Track, Off Track, Completed

        [Required]
        [StringLength(20)]
        public string Priority { get; set; } = "Medium"; // Low, Medium, High

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
