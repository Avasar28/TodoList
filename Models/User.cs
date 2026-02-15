using System;
using System.ComponentModel.DataAnnotations;

namespace TodoListApp.Models
{
    public class User
    {
        public int Id { get; set; }

        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        public string Password { get; set; } = string.Empty; // In real app, store simple hash

        public string? ResetToken { get; set; }
        public DateTime? ResetTokenExpiry { get; set; }

        public bool IsEmailVerified { get; set; } = false;

        public string? Name { get; set; }
        public bool IsAdmin { get; set; } = false;
        public UserPreferences Preferences { get; set; } = new UserPreferences();
    }

    public class UserPreferences
    {
        public string? DefaultCity { get; set; }
        public string? DefaultFromCurrency { get; set; }
        public string? DefaultToCurrency { get; set; }
        public string? DefaultSourceTimeZone { get; set; }
        public string? DefaultTargetTimeZone { get; set; }
        
        public List<string> FavoriteCities { get; set; } = new List<string>();
        public List<string> FavoriteCurrencyPairs { get; set; } = new List<string>(); // Format: "USD-EUR"
        
        // PDF Tools
        public List<string> FavoritePdfTools { get; set; } = new List<string>();
        public bool AutoDeletePdfEnabled { get; set; } = true;
    }
}
