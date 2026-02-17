using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TodoListApp.Data;
using TodoListApp.Models;
using TodoListApp.ViewModels;
using TodoListApp.Helpers;

namespace TodoListApp.Services
{
    public class UserManagementService : IUserManagementService
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _context;
        private readonly IEmailService _emailService;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public UserManagementService(
            UserManager<ApplicationUser> userManager,
            ApplicationDbContext context,
            IEmailService emailService,
            IHttpContextAccessor httpContextAccessor)
        {
            _userManager = userManager;
            _context = context;
            _emailService = emailService;
            _httpContextAccessor = httpContextAccessor;
        }

        public async Task<IEnumerable<SystemFeature>> GetAllFeaturesAsync()
        {
            return await _context.SystemFeatures.OrderBy(f => f.Type).ThenBy(f => f.Name).ToListAsync();
        }

        public async Task<(bool Success, string Message, string? RedirectUrl)> CreateUserAsync(AdminCreateUserViewModel model, string? grantedBy)
        {
            var existingUser = await _userManager.FindByEmailAsync(model.Email);
            if (existingUser != null)
            {
                return (false, "Email already exists.", null);
            }

            // Generate OTP
            var otp = OtpHelper.Generate6DigitOtp();
            var expiry = DateTime.UtcNow.AddMinutes(10);

            // Store in Session
            var session = _httpContextAccessor.HttpContext?.Session;
            if (session == null) return (false, "Session not available.", null);

            session.SetString("SignupEmail", model.Email);
            session.SetString("SignupPassword", model.Password);
            session.SetString("SignupFullName", model.Name);
            session.SetString("SignupRole", model.Role);
            session.SetString("SignupOtp", otp);
            session.SetString("SignupOtpExpiry", expiry.ToString("O"));
            
            // Store feature IDs as a comma-separated string in session
            if (model.SelectedFeatureIds != null && model.SelectedFeatureIds.Any())
            {
                session.SetString("SignupFeatures", string.Join(",", model.SelectedFeatureIds));
            }

            // Store Passkey info in session
            session.SetString("SignupIsPasskeyEnabled", model.IsPasskeyEnabled.ToString());
            if (model.IsPasskeyEnabled && !string.IsNullOrEmpty(model.Pin))
            {
                var hasher = new PasswordHasher<ApplicationUser>();
                var dummyUser = new ApplicationUser { Email = model.Email };
                var hashedPin = hasher.HashPassword(dummyUser, model.Pin);
                session.SetString("SignupPasskeyHash", hashedPin);
            }

            // Send Email
            await _emailService.SendEmailAsync(model.Email, "Verify Your Email", OtpHelper.GetOtpEmailBody(otp));

            return (true, "OTP sent successfully!", "/Account/VerifyOtp");
        }
    }
}
