using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using TodoListApp.Models;

namespace TodoListApp.Controllers
{
    public class PasskeyController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IPasswordHasher<ApplicationUser> _passwordHasher;
        private readonly IMemoryCache _cache;
        private readonly ILogger<PasskeyController> _logger;

        public PasskeyController(
            UserManager<ApplicationUser> userManager, 
            IPasswordHasher<ApplicationUser> passwordHasher,
            IMemoryCache cache,
            ILogger<PasskeyController> logger)
        {
            _userManager = userManager;
            _passwordHasher = passwordHasher;
            _cache = cache;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> Verify(string returnUrl)
        {
            ViewBag.ReturnUrl = returnUrl;
            return await Task.FromResult(View());
        }

        [HttpPost]
        public async Task<IActionResult> ValidatePin(string pin, string returnUrl)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null || !user.IsPasskeyEnabled)
            {
                 return Redirect(returnUrl ?? "/");
            }

            string cacheKey = $"passkey_attempts_{user.Id}";
            int attempts = _cache.Get<int?>(cacheKey) ?? 0;

            if (attempts >= 5)
            {
                ModelState.AddModelError(string.Empty, "Too many failed attempts. Please try again later.");
                ViewBag.ReturnUrl = returnUrl;
                return View("Verify");
            }

            var result = _passwordHasher.VerifyHashedPassword(user, user.PasskeyHash!, pin);
            if (result == PasswordVerificationResult.Success)
            {
                _cache.Remove(cacheKey);
                
                // Set a very short-lived ephemeral cookie to allow ONLY the immediate redirect to pass.
                var cookieOptions = new CookieOptions
                {
                    HttpOnly = true,
                    Secure = true,
                    Expires = DateTimeOffset.UtcNow.AddSeconds(10) // 10 seconds to satisfy the redirect
                };
                Response.Cookies.Append("PasskeyVerified", "true", cookieOptions);

                return Redirect(returnUrl ?? "/");
            }

            // Failed attempt
            attempts++;
            _cache.Set(cacheKey, attempts, TimeSpan.FromMinutes(15));
            _logger.LogWarning("Failed passkey attempt for user {UserId}. Attempt {Count}/5", user.Id, attempts);

            ModelState.AddModelError(string.Empty, $"Invalid PIN. {5 - attempts} attempts remaining.");
            ViewBag.ReturnUrl = returnUrl;
            return View("Verify");
        }

        [HttpGet]
        public IActionResult ForgotPin(string returnUrl)
        {
            ViewBag.ReturnUrl = returnUrl;
            return View();
        }

        [HttpGet]
        public IActionResult VerifyPassword(string returnUrl)
        {
            ViewBag.ReturnUrl = returnUrl;
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> ConfirmPassword(string password, string returnUrl)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction("Login", "Account");

            var result = await _userManager.CheckPasswordAsync(user, password);
            if (result)
            {
                HttpContext.Session.SetString("PasskeyRecoveryAuthenticated", "true");
                return RedirectToAction("ResetPin", new { returnUrl });
            }

            ModelState.AddModelError(string.Empty, "Incorrect account password.");
            ViewBag.ReturnUrl = returnUrl;
            return View("VerifyPassword");
        }

        [HttpPost]
        public async Task<IActionResult> SendRecoveryOtp(string returnUrl)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null || string.IsNullOrEmpty(user.Email)) return RedirectToAction("Login", "Account");

            var otp = Helpers.OtpHelper.Generate6DigitOtp();
            var expiry = DateTime.UtcNow.AddMinutes(10);

            HttpContext.Session.SetString("PasskeyRecoveryEmail", user.Email);
            HttpContext.Session.SetString("PasskeyRecoveryOtp", otp);
            HttpContext.Session.SetString("PasskeyRecoveryOtpExpiry", expiry.ToString("O"));

            var emailService = HttpContext.RequestServices.GetRequiredService<Services.IEmailService>();
            await emailService.SendEmailAsync(user.Email, "PIN Recovery OTP", Helpers.OtpHelper.GetOtpEmailBody(otp));

            return RedirectToAction("VerifyRecoveryOtp", new { returnUrl });
        }

        [HttpGet]
        public IActionResult VerifyRecoveryOtp(string returnUrl)
        {
            var email = HttpContext.Session.GetString("PasskeyRecoveryEmail");
            if (string.IsNullOrEmpty(email)) return RedirectToAction("ForgotPin", new { returnUrl });

            ViewBag.ReturnUrl = returnUrl;
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> VerifyRecoveryOtp(string otp, string returnUrl)
        {
            var sessionOtp = HttpContext.Session.GetString("PasskeyRecoveryOtp");
            var sessionExpiryStr = HttpContext.Session.GetString("PasskeyRecoveryOtpExpiry");

            if (string.IsNullOrEmpty(sessionOtp) || string.IsNullOrEmpty(sessionExpiryStr))
            {
                return RedirectToAction("ForgotPin", new { returnUrl });
            }

            DateTime sessionExpiry = DateTime.Parse(sessionExpiryStr);

            if (otp == sessionOtp && DateTime.UtcNow <= sessionExpiry)
            {
                HttpContext.Session.SetString("PasskeyRecoveryAuthenticated", "true");
                HttpContext.Session.Remove("PasskeyRecoveryOtp");
                HttpContext.Session.Remove("PasskeyRecoveryOtpExpiry");
                return RedirectToAction("ResetPin", new { returnUrl });
            }

            ModelState.AddModelError(string.Empty, "Invalid or expired OTP.");
            ViewBag.ReturnUrl = returnUrl;
            return View();
        }

        [HttpGet]
        public IActionResult ResetPin(string returnUrl)
        {
            if (HttpContext.Session.GetString("PasskeyRecoveryAuthenticated") != "true")
            {
                return RedirectToAction("Verify", new { returnUrl });
            }

            ViewBag.ReturnUrl = returnUrl;
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> UpdatePin(string pin, string confirmPin, string returnUrl)
        {
            if (HttpContext.Session.GetString("PasskeyRecoveryAuthenticated") != "true")
            {
                return RedirectToAction("Verify", new { returnUrl });
            }

            if (string.IsNullOrEmpty(pin) || (pin.Length != 4 && pin.Length != 6))
            {
                ModelState.AddModelError(string.Empty, "PIN must be 4 or 6 digits.");
            }
            else if (pin != confirmPin)
            {
                ModelState.AddModelError(string.Empty, "PINs do not match.");
            }

            if (!ModelState.IsValid)
            {
                ViewBag.ReturnUrl = returnUrl;
                return View("ResetPin");
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction("Login", "Account");

            user.PasskeyHash = _passwordHasher.HashPassword(user, pin);
            user.IsPasskeyEnabled = true;

            var result = await _userManager.UpdateAsync(user);
            if (result.Succeeded)
            {
                HttpContext.Session.Remove("PasskeyRecoveryAuthenticated");
                TempData["SuccessMessage"] = "Passkey PIN has been reset successfully.";
                return RedirectToAction("Verify", new { returnUrl });
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            ViewBag.ReturnUrl = returnUrl;
            return View("ResetPin");
        }
    }
}
