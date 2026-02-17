using System.ComponentModel.DataAnnotations;

namespace TodoListApp.ViewModels
{
    public class AdminCreateUserViewModel
    {
        [Required(ErrorMessage = "Full Name is required")]
        [Display(Name = "Full Name")]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Invalid email format")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Password is required")]
        [MinLength(8, ErrorMessage = "Password must be at least 8 characters")]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        [Required(ErrorMessage = "Please confirm your password")]
        [DataType(DataType.Password)]
        [Compare("Password", ErrorMessage = "Passwords do not match")]
        [Display(Name = "Confirm Password")]
        public string ConfirmPassword { get; set; } = string.Empty;

        [Required(ErrorMessage = "Please select a role")]
        public string Role { get; set; } = "NormalUser";

        public List<int> SelectedFeatureIds { get; set; } = new();

        public bool IsPasskeyEnabled { get; set; }

        [RegularExpression(@"^\d{4}$|^\d{6}$", ErrorMessage = "PIN must be 4 or 6 digits")]
        public string? Pin { get; set; }

        [Compare("Pin", ErrorMessage = "PINs do not match")]
        public string? ConfirmPin { get; set; }
    }
}
