using System.ComponentModel.DataAnnotations;

namespace TodoListApp.Models
{
    public enum FeatureType
    {
        Page,
        Widget
    }

    public class SystemFeature
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [StringLength(500)]
        public string Description { get; set; } = string.Empty;

        [StringLength(50)]
        public string Icon { get; set; } = "📌";

        [Required]
        public FeatureType Type { get; set; }

        [Required]
        public string TechnicalName { get; set; } = string.Empty;

        public bool IsDefault { get; set; }
    }
}
