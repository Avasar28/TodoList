using System;
using System.ComponentModel.DataAnnotations;

namespace TodoListApp.Models
{
    public class User
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        public string Password { get; set; } = string.Empty; // In real app, store simple hash

        public string? ResetToken { get; set; }
        public DateTime? ResetTokenExpiry { get; set; }

        public bool IsEmailVerified { get; set; } = false;
    }
}
