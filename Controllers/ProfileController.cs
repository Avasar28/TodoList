using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using TodoListApp.Models;
using TodoListApp.Services;

namespace TodoListApp.Controllers
{
    [Authorize]
    public class ProfileController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IWebAuthnService _webAuthnService;
        private readonly IPasswordHasher<ApplicationUser> _passwordHasher;

        public ProfileController(
            UserManager<ApplicationUser> userManager,
            IWebAuthnService webAuthnService,
            IPasswordHasher<ApplicationUser> passwordHasher)
        {
            _userManager = userManager;
            _webAuthnService = webAuthnService;
            _passwordHasher = passwordHasher;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction("Login", "Account");
            return View(user);
        }

        [HttpGet]
        public async Task<IActionResult> Security()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction("Login", "Account");

            ViewBag.Credentials = await _webAuthnService.GetCredentialsAsync(user.Id);
            return View(user);
        }

        [HttpPost]
        public async Task<IActionResult> UpdatePin(string pin, string confirmPin)
        {
            if (string.IsNullOrEmpty(pin) || (pin.Length != 4 && pin.Length != 6))
            {
                TempData["ErrorMessage"] = "PIN must be 4 or 6 digits.";
                return RedirectToAction(nameof(Security));
            }

            if (pin != confirmPin)
            {
                TempData["ErrorMessage"] = "PINs do not match.";
                return RedirectToAction(nameof(Security));
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction("Login", "Account");

            user.PasskeyHash = _passwordHasher.HashPassword(user, pin);
            user.IsPasskeyEnabled = true;

            var result = await _userManager.UpdateAsync(user);
            if (result.Succeeded)
            {
                TempData["SuccessMessage"] = "PIN updated successfully.";
            }
            else
            {
                TempData["ErrorMessage"] = "Failed to update PIN: " + string.Join(", ", result.Errors.Select(e => e.Description));
            }

            return RedirectToAction(nameof(Security));
        }

        [HttpPost]
        public async Task<IActionResult> DisablePin()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction("Login", "Account");

            user.IsPasskeyEnabled = false;
            user.PasskeyHash = null;

            var result = await _userManager.UpdateAsync(user);
            if (result.Succeeded)
            {
                TempData["SuccessMessage"] = "PIN protection disabled.";
            }
            else
            {
                TempData["ErrorMessage"] = "Failed to disable PIN.";
            }

            return RedirectToAction(nameof(Security));
        }

        [HttpPost]
        public async Task<IActionResult> RemoveBiometric(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction("Login", "Account");

            var success = await _webAuthnService.RemoveCredentialAsync(id, user.Id);
            if (success)
            {
                TempData["SuccessMessage"] = "Biometric device removed.";
            }
            else
            {
                TempData["ErrorMessage"] = "Failed to remove biometric device.";
            }

            return RedirectToAction(nameof(Security));
        }
    }
}
