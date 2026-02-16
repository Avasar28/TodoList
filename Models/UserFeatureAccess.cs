using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TodoListApp.Models
{
    public class UserFeatureAccess
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string UserId { get; set; } = string.Empty;

        [ForeignKey("UserId")]
        public virtual ApplicationUser? User { get; set; }

        [Required]
        public int FeatureId { get; set; }

        [ForeignKey("FeatureId")]
        public virtual SystemFeature? Feature { get; set; }

        [StringLength(100)]
        public string? GrantedBy { get; set; }

        public DateTime GrantedAt { get; set; } = DateTime.UtcNow;
    }
}
