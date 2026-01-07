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

        public async Task<IActionResult> Dashboard(string? city, string? fromCurrency, string? toCurrency, string? sourceTime, string? targetTime)
        {
            var userId = GetUserId();
            var user = _userService.GetUser(userId);
            
            // Apply Defaults if params are missing
            if (user != null)
            {
                city ??= user.Preferences.DefaultCity;
                fromCurrency ??= user.Preferences.DefaultFromCurrency ?? "USD";
                toCurrency ??= user.Preferences.DefaultToCurrency ?? "EUR";
                sourceTime ??= user.Preferences.DefaultSourceTimeZone ?? "UTC";
                targetTime ??= user.Preferences.DefaultTargetTimeZone;
            }

            // Auto-Detect if still null (User didn't set preference OR not logged in)
            if (string.IsNullOrEmpty(city) || string.IsNullOrEmpty(targetTime))
            {
                var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "";
                // Forwarded header support for proxies (e.g. if behind Nginx/IIS)
                if (Request.Headers.ContainsKey("X-Forwarded-For"))
                    ip = Request.Headers["X-Forwarded-For"].FirstOrDefault() ?? "";

                var location = await _externalApiService.GetLocationFromIpAsync(ip);
                city ??= location.City;
                targetTime ??= location.TimeZoneId;
            }

            // Fallbacks
            city ??= "London";
            fromCurrency ??= "USD";
            toCurrency ??= "EUR";
            sourceTime ??= "UTC";
            targetTime ??= "GMT Standard Time";

            // Validate TimeZone Ids to prevent crash if default doesn't exist on OS
            if (sourceTime == "GMT Standard Time" && TimeZoneInfo.FindSystemTimeZoneById("UTC") != null) targetTime = "UTC"; 
            
            var weatherTask = _externalApiService.GetWeatherAsync(city);
            var currencyTask = _externalApiService.GetCurrencyRateAsync(fromCurrency, toCurrency);
            var timeTask = _externalApiService.GetTimeConversionAsync(sourceTime, targetTime);

            await Task.WhenAll(weatherTask, currencyTask, timeTask);

            var model = new ViewModels.DashboardViewModel
            {
                UserName = user?.Name ?? user?.Email.Split('@')[0] ?? "User",
                Preferences = user?.Preferences ?? new UserPreferences(),
                Weather = await weatherTask,
                Currency = await currencyTask,
                TimeConversion = await timeTask,
                SelectedCity = city,
                FromCurrency = fromCurrency,
                ToCurrency = toCurrency,
                SourceTimeZone = sourceTime,
                TargetTimeZone = targetTime,
                AvailableTimeZones = TimeZoneInfo.GetSystemTimeZones().Select(z => z.Id).OrderBy(z => z).ToList()
            };

            return View(model);
        }

        [HttpPost]
        public IActionResult UpdatePreferences([FromBody] UserPreferences preferences)
        {
            var userId = GetUserId();
            if (_userService.UpdatePreferences(userId, preferences))
            {
                return Ok();
            }
            return BadRequest();
        }

        // --- JSON API Endpoints for AJAX ---

        [HttpGet]
        public async Task<IActionResult> GetWeatherJson(string city)
        {
            var data = await _externalApiService.GetWeatherAsync(city);
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
            return View();
        }

        [HttpPost]
        public IActionResult CreateUser(TodoListApp.Models.User user)
        {
            // Simple validation
            if (string.IsNullOrWhiteSpace(user.Email) || string.IsNullOrWhiteSpace(user.Password))
            {
                ModelState.AddModelError("", "Email and Password are required");
                return View(user);
            }

            if (_userService.RegisterUser(user.Email, user.Password, user.Name ?? "", isVerified: true))
            {
                return RedirectToAction(nameof(UserList));
            }
            
            ModelState.AddModelError("", "User creation failed (email might be taken)");
            return View(user);
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
