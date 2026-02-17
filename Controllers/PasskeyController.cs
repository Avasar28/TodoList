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
    }
}
