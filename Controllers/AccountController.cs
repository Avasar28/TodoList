using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TodoListApp.Models;
using TodoListApp.Services;

namespace TodoListApp.Controllers
{
    public class AccountController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly IEmailService _emailService;
        private readonly IConfiguration _config;

        public AccountController(
            UserManager<ApplicationUser> userManager, 
            SignInManager<ApplicationUser> signInManager, 
            IEmailService emailService, 
            IConfiguration config)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _emailService = emailService;
            _config = config;
        }

        [HttpGet]
        public IActionResult Login()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Login(ViewModels.LoginViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var result = await _signInManager.PasswordSignInAsync(model.Email, model.Password, isPersistent: false, lockoutOnFailure: false);
            if (result.Succeeded)
            {
                return RedirectToAction("Dashboard", "Todo");
            }

            ModelState.AddModelError(string.Empty, "Invalid email or password");
            return View(model);
        }

        [HttpGet]
        public IActionResult Signup()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Signup(ViewModels.SignupViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            // Check if user already exists
            var existingUser = await _userManager.FindByEmailAsync(model.Email);
            if (existingUser != null) 
            {
                ModelState.AddModelError(string.Empty, "Email already exists");
                return View(model);
            }

            // Generate 6-digit OTP
            var otp = Helpers.OtpHelper.Generate6DigitOtp();
            var expiry = DateTime.UtcNow.AddMinutes(10);
            
            // Store EVERYTHING in session (Email, Password, OTP, Expiry)
            HttpContext.Session.SetString("SignupEmail", model.Email);
            HttpContext.Session.SetString("SignupPassword", model.Password);
            HttpContext.Session.SetString("SignupFullName", model.FullName);
            HttpContext.Session.SetString("SignupOtp", otp);
            HttpContext.Session.SetString("SignupOtpExpiry", expiry.ToString("O")); // ISO 8601

            // Send OTP email
            await _emailService.SendEmailAsync(model.Email, "Verify Your Email", Helpers.OtpHelper.GetOtpEmailBody(otp));

            return RedirectToAction("VerifyOtp");
        }

        [HttpGet]
        public IActionResult VerifyOtp()
        {
            var email = HttpContext.Session.GetString("SignupEmail");
            if (string.IsNullOrEmpty(email)) return RedirectToAction("Signup");

            return View(new ViewModels.VerifyOtpViewModel { Email = email });
        }

        [HttpPost]
        public async Task<IActionResult> VerifyOtp(ViewModels.VerifyOtpViewModel model)
        {
            var email = HttpContext.Session.GetString("SignupEmail");
            var password = HttpContext.Session.GetString("SignupPassword");
            var fullName = HttpContext.Session.GetString("SignupFullName") ?? "Unknown";
            var sessionOtp = HttpContext.Session.GetString("SignupOtp");
            var sessionExpiryStr = HttpContext.Session.GetString("SignupOtpExpiry");

            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password) || string.IsNullOrEmpty(sessionOtp) || string.IsNullOrEmpty(sessionExpiryStr))
            {
                return RedirectToAction("Signup");
            }

            DateTime sessionExpiry = DateTime.Parse(sessionExpiryStr);

            if (model.Otp == sessionOtp && DateTime.UtcNow <= sessionExpiry)
            {
                var role = HttpContext.Session.GetString("SignupRole") ?? "NormalUser";

                // OTP verified! Now actually create the user.
                var user = new ApplicationUser 
                { 
                    UserName = email, 
                    Email = email, 
                    Name = fullName,
                    EmailConfirmed = true 
                };

                var result = await _userManager.CreateAsync(user, password);
                if (result.Succeeded)
                {
                    // Assign the selected role
                    await _userManager.AddToRoleAsync(user, role);

                    // Save Feature Access if any
                    var featuresStr = HttpContext.Session.GetString("SignupFeatures");
                    if (!string.IsNullOrEmpty(featuresStr))
                    {
                        var featureIds = featuresStr.Split(',').Select(int.Parse).ToList();
                        var context = HttpContext.RequestServices.GetRequiredService<Data.ApplicationDbContext>();
                        var currentAdmin = User.Identity?.Name ?? "System";

                        foreach (var fId in featureIds)
                        {
                            context.UserFeatures.Add(new UserFeatureAccess
                            {
                                UserId = user.Id,
                                FeatureId = fId,
                                GrantedBy = currentAdmin,
                                GrantedAt = DateTime.UtcNow
                            });
                        }
                        await context.SaveChangesAsync();
                    }

                    // Clear session
                    HttpContext.Session.Remove("SignupEmail");
                    HttpContext.Session.Remove("SignupPassword");
                    HttpContext.Session.Remove("SignupFullName");
                    HttpContext.Session.Remove("SignupRole");
                    HttpContext.Session.Remove("SignupFeatures");
                    HttpContext.Session.Remove("SignupOtp");
                    HttpContext.Session.Remove("SignupOtpExpiry");

                    TempData["SuccessMessage"] = "Account created and email verified successfully!";

                    // Smart Redirect: If Admin is creating user, go back to User List. If new user signup, go to Login.
                    if (User.Identity != null && User.Identity.IsAuthenticated)
                    {
                        return RedirectToAction("UserList", "Todo");
                    }
                    else
                    {
                        return RedirectToAction("Login");
                    }
                }
                
                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
            }
            else if (DateTime.UtcNow > sessionExpiry)
            {
                ModelState.AddModelError(string.Empty, "OTP has expired. Please signup again.");
            }
            else
            {
                ModelState.AddModelError(string.Empty, "Invalid OTP code.");
            }
            
            model.Email = email ?? string.Empty;
            return View(model);
        }

        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            return RedirectToAction("Login");
        }

        [HttpGet]
        public IActionResult ForgotPassword()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> ForgotPassword(ViewModels.ForgotPasswordViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var user = await _userManager.FindByEmailAsync(model.Email);
            if (user != null)
            {
                var token = await _userManager.GeneratePasswordResetTokenAsync(user);
                
                // Try to get BaseUrl from config, fallback to current request context
                var baseUrl = _config["BaseUrl"]?.TrimEnd('/');
                var actionUrl = Url.Action("ResetPassword", "Account", new { email = model.Email, token = token });
                
                string resetLink;
                if (!string.IsNullOrEmpty(baseUrl))
                {
                    resetLink = $"{baseUrl}{actionUrl}";
                }
                else
                {
                    resetLink = Url.Action("ResetPassword", "Account", 
                        new { email = model.Email, token = token }, 
                        Request.Scheme, 
                        Request.Host.Value)!;
                }

                await _emailService.SendEmailAsync(model.Email, "Reset Your Password", Helpers.OtpHelper.GetResetPasswordEmailBody(resetLink));
            }

            TempData["SuccessMessage"] = "If an account exists with that email, a reset link has been sent.";
            return RedirectToAction("Login");
        }

        [HttpGet]
        public IActionResult ResetPassword(string email, string token)
        {
            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(token))
            {
                return RedirectToAction("Login");
            }

            return View(new ViewModels.ResetPasswordViewModel { Email = email, Token = token });
        }

        [HttpPost]
        public async Task<IActionResult> ResetPassword(ViewModels.ResetPasswordViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var user = await _userManager.FindByEmailAsync(model.Email);
            if (user != null)
            {
                var result = await _userManager.ResetPasswordAsync(user, model.Token, model.NewPassword);
                if (result.Succeeded)
                {
                    TempData["SuccessMessage"] = "Your password has been reset successfully. Please login.";
                    return RedirectToAction("Login");
                }

                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
            }
            else
            {
                ModelState.AddModelError(string.Empty, "Invalid email address.");
            }

            return View(model);
        }
        [HttpGet]
        public IActionResult AccessDenied()
        {
            return View();
        }
    }
}
