using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TodoListApp.Models;
using TodoListApp.Services;

namespace TodoListApp.Controllers
{
    [Authorize]
    public class TodoController : Controller
    {
        private readonly ITodoService _todoService;
        private readonly IExternalApiService _externalApiService;

        private readonly IUserService _userService;

        public TodoController(ITodoService todoService, IExternalApiService externalApiService, IUserService userService)
        {
            _todoService = todoService;
            _externalApiService = externalApiService;
            _userService = userService;
        }

        public IActionResult Dashboard(string? city, string? fromCurrency, string? toCurrency, string? sourceTime, string? targetTime)
        {
            var userId = GetUserId();
            var user = _userService.GetUser(userId);
            
            // Apply Defaults if params are missing
            if (user != null)
            {
                city ??= user.Preferences.DefaultCity;
                fromCurrency ??= user.Preferences.DefaultFromCurrency;
                toCurrency ??= user.Preferences.DefaultToCurrency;
                sourceTime ??= user.Preferences.DefaultSourceTimeZone;
                targetTime ??= user.Preferences.DefaultTargetTimeZone;
            }

            // Fallbacks (Immediate)
            city ??= "London";
            fromCurrency ??= "USD";
            toCurrency ??= "EUR";
            sourceTime ??= "UTC";
            targetTime ??= "GMT Standard Time";

            var model = new ViewModels.DashboardViewModel
            {
                UserName = user?.Name ?? user?.Email.Split('@')[0] ?? "User",
                Preferences = user?.Preferences ?? new UserPreferences(),
                // Keep default empty objects for Weather, Currency, TimeConversion to avoid nulls in View
                SelectedCity = city,
                FromCurrency = fromCurrency,
                ToCurrency = toCurrency,
                SourceTimeZone = sourceTime,
                TargetTimeZone = targetTime,
                AvailableTimeZones = TimeZoneInfo.GetSystemTimeZones()
                    .Select(z => new ViewModels.TimeZoneOption { Id = z.Id, Name = z.DisplayName })
                    .OrderBy(z => z.Name)
                    .ToList()
            };

            return View(model);
        }


        // --- JSON API Endpoints for AJAX ---

        [HttpGet]
        public async Task<IActionResult> GetWeatherJson(string city)
        {
            var data = await _externalApiService.GetWeatherAsync(city);
            return Json(data);
        }

        [HttpGet]
        public async Task<IActionResult> GetWeatherByCoordsJson(double lat, double lon)
        {
            var data = await _externalApiService.GetWeatherByCoordsAsync(lat, lon);
            return Json(data);
        }

        [HttpGet]
        public async Task<IActionResult> GetCurrencyJson(string from, string to)
        {
            var data = await _externalApiService.GetCurrencyRateAsync(from, to);
            return Json(data);
        }

        [HttpGet]
        public async Task<IActionResult> GetCurrencyHistoryJson(string from, string to)
        {
            var data = await _externalApiService.GetCurrencyHistoryAsync(from, to);
            return Json(data);
        }

        [HttpGet]
        public async Task<IActionResult> GetTimeJson(string source, string target, string? time = null)
        {
            var data = await _externalApiService.GetTimeConversionAsync(source, target, time);
            return Json(data);
        }

        [HttpGet]
        public async Task<IActionResult> GetLocationJson()
        {
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "";
            if (Request.Headers.ContainsKey("X-Forwarded-For"))
                ip = Request.Headers["X-Forwarded-For"].FirstOrDefault() ?? "";

            var data = await _externalApiService.GetLocationFromIpAsync(ip);
            return Json(data);
        }

        private int GetUserId()
        {
            var idClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (idClaim != null && int.TryParse(idClaim.Value, out var id))
            {
                return id;
            }
            throw new Exception("User ID not found");
        }

        public IActionResult Index(string searchString, string status)
        {
            ViewData["CurrentFilter"] = searchString;
            ViewData["CurrentStatus"] = status;
            
            var items = _todoService.GetAll(GetUserId());
            var today = DateTime.Today;

            if (!string.IsNullOrEmpty(searchString))
            {
                items = items.Where(s => s.Title.Contains(searchString, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrEmpty(status))
            {
                items = status switch
                {
                    "Completed" => items.Where(t => t.IsCompleted),
                    "Pending" => items.Where(t => !t.IsCompleted),
                    "Overdue" => items.Where(t => !t.IsCompleted && t.DueDate.HasValue && t.DueDate.Value.Date < today),
                    _ => items
                };
            }

            return View(items);
        }

        public IActionResult UserList()
        {
            var users = _userService.GetAllUsers();
            return View(users);
        }

        [HttpGet]
        public IActionResult CreateUser()
        {
            return View(new ViewModels.AdminCreateUserViewModel());
        }

        [HttpPost]
        public async Task<IActionResult> CreateUser(ViewModels.AdminCreateUserViewModel model, [FromServices] IEmailService emailService)
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
            // Using same keys as AccountController.Signup so VerifyOtp works
            HttpContext.Session.SetString("SignupEmail", model.Email);
            HttpContext.Session.SetString("SignupPassword", model.Password);
            HttpContext.Session.SetString("SignupFullName", model.Name);
            HttpContext.Session.SetString("SignupOtp", otp);
            HttpContext.Session.SetString("SignupOtpExpiry", expiry.ToString("O")); // ISO 8601

            // Send OTP email
            await emailService.SendEmailAsync(model.Email, "Verify Your Email", Helpers.OtpHelper.GetOtpEmailBody(otp));

            return RedirectToAction("VerifyOtp", "Account");
        }

        [HttpGet]
        public IActionResult EditUser(int id)
        {
            var user = _userService.GetUser(id);
            if (user == null) return NotFound();
            return View(user);
        }

        [HttpPost]
        public IActionResult EditUser(TodoListApp.Models.User user)
        {
            if (_userService.UpdateUser(user))
            {
                return RedirectToAction(nameof(UserList));
            }
            ModelState.AddModelError("", "Update failed");
            return View(user);
        }

        [HttpPost]
        public IActionResult DeleteUser(int id)
        {
            _userService.DeleteUser(id);
            return RedirectToAction(nameof(UserList));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create(string title, DateTime? dueDate, string priority = "Medium")
        {
            if (!string.IsNullOrWhiteSpace(title))
            {
                var newItem = new TodoItem 
                { 
                    Title = title,
                    UserId = GetUserId(),
                    DueDate = dueDate,
                    Priority = priority
                };
                _todoService.Create(newItem);
            }
            
            var referer = Request.Headers["Referer"].ToString();
            if (!string.IsNullOrEmpty(referer))
            {
                return Redirect(referer);
            }
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Toggle(Guid id)
        {
            _todoService.ToggleComplete(id, GetUserId());
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Delete(Guid id)
        {
            _todoService.Delete(id, GetUserId());
            return RedirectToAction(nameof(Index));
        }

        public IActionResult Edit(Guid id)
        {
            var item = _todoService.GetById(id, GetUserId());
            if (item == null)
            {
                return NotFound();
            }
            return View(item);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Edit(TodoItem item)
        {
            if (ModelState.IsValid)
            {
                _todoService.Update(item, GetUserId());
                return RedirectToAction(nameof(Index));
            }
            return View(item);
        }
    }
}
