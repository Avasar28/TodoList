using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TodoListApp.Models;
using TodoListApp.Services;

namespace TodoListApp.Controllers
{
    public class AccountController : Controller
    {
        private readonly IUserService _userService;
        private readonly IEmailService _emailService;
        private readonly IConfiguration _config;

        public AccountController(IUserService userService, IEmailService emailService, IConfiguration config)
        {
            _userService = userService;
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

            var user = _userService.ValidateUser(model.Email, model.Password);
            if (user != null)
            {
                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.Name, user.Email),
                    new Claim(ClaimTypes.NameIdentifier, user.Id.ToString())
                };

                var identity = new ClaimsIdentity(claims, "CookieAuth");
                var principal = new ClaimsPrincipal(identity);

                await HttpContext.SignInAsync("CookieAuth", principal);

                return RedirectToAction("Index", "Todo");
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
            if (_userService.UserExists(model.Email)) 
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
        public IActionResult VerifyOtp(ViewModels.VerifyOtpViewModel model)
        {
            var email = HttpContext.Session.GetString("SignupEmail");
            var password = HttpContext.Session.GetString("SignupPassword");
            var sessionOtp = HttpContext.Session.GetString("SignupOtp");
            var sessionExpiryStr = HttpContext.Session.GetString("SignupOtpExpiry");

            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password) || string.IsNullOrEmpty(sessionOtp) || string.IsNullOrEmpty(sessionExpiryStr))
            {
                return RedirectToAction("Signup");
            }

            DateTime sessionExpiry = DateTime.Parse(sessionExpiryStr);

            if (model.Otp == sessionOtp && DateTime.UtcNow <= sessionExpiry)
            {
                // OTP verified! Now actually create the user.
                if (_userService.RegisterUser(email, password, isVerified: true))
                {
                    // Clear session and redirect to login
                    HttpContext.Session.Remove("SignupEmail");
                    HttpContext.Session.Remove("SignupPassword");
                    HttpContext.Session.Remove("SignupOtp");
                    HttpContext.Session.Remove("SignupOtpExpiry");

                    TempData["SuccessMessage"] = "Account created and email verified successfully! You can now login.";
                    return RedirectToAction("Login");
                }
                ModelState.AddModelError(string.Empty, "Registration failed. Please try again.");
            }
            else if (DateTime.UtcNow > sessionExpiry)
            {
                ModelState.AddModelError(string.Empty, "OTP has expired. Please signup again.");
            }
            else
            {
                ModelState.AddModelError(string.Empty, "Invalid OTP code.");
            }
            
            model.Email = email;
            return View(model);
        }

        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync("CookieAuth");
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

            var token = _userService.GenerateResetToken(model.Email);
            if (token != null)
            {
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

                Console.WriteLine("==================================================");
                Console.WriteLine("   PASSWORD RESET LINK GENERATED");
                Console.WriteLine("==================================================");
                Console.WriteLine($"Configured BaseUrl: {baseUrl ?? "NONE (using auto-detection)"}");
                Console.WriteLine($"Actual Browser URL: {Request.Scheme}://{Request.Host}");
                Console.WriteLine($"Generated Link:     {resetLink}");
                Console.WriteLine("--------------------------------------------------");
                Console.WriteLine("TIP: If clicking the link fails, check if the PORT");
                Console.WriteLine("matches YOUR browser. If your browser forces HTTPS,");
                Console.WriteLine("change 'http' to 'https' and port 5153 to 7130.");
                Console.WriteLine("==================================================");
                
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
        public IActionResult ResetPassword(ViewModels.ResetPasswordViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            if (_userService.ResetPasswordWithToken(model.Email, model.Token, model.NewPassword))
            {
                TempData["SuccessMessage"] = "Your password has been reset successfully. Please login.";
                return RedirectToAction("Login");
            }

            ModelState.AddModelError(string.Empty, "Invalid or expired reset token. Please request a new one.");
            return View(model);
        }
    }
}
