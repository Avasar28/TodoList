using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

namespace TodoListApp.Models
{
    public class ApplicationUser : IdentityUser
    {
        public string? Name { get; set; }

        // Store UserPreferences as a JSON string in the database
        public string? PreferencesJson { get; set; }

        [NotMapped]
        public UserPreferences Preferences
        {
            get => string.IsNullOrEmpty(PreferencesJson) 
                   ? new UserPreferences() 
                   : JsonSerializer.Deserialize<UserPreferences>(PreferencesJson) ?? new UserPreferences();
            set => PreferencesJson = JsonSerializer.Serialize(value);
        }
    }
}
