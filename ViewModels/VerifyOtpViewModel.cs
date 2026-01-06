using System.ComponentModel.DataAnnotations;

namespace TodoListApp.ViewModels
{
    public class VerifyOtpViewModel
    {
        [Required(ErrorMessage = "OTP is required")]
        [StringLength(6, MinimumLength = 6, ErrorMessage = "OTP must be 6 digits")]
        [RegularExpression("^[0-9]*$", ErrorMessage = "OTP must be numeric")]
        public string Otp { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;
    }
}
