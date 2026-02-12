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
        private readonly IWebHostEnvironment _webHostEnvironment;

        public TodoController(ITodoService todoService, IExternalApiService externalApiService, IUserService userService, IWebHostEnvironment webHostEnvironment)
        {
            _todoService = todoService;
            _externalApiService = externalApiService;
            _userService = userService;
            _webHostEnvironment = webHostEnvironment;
        }

        public async Task<IActionResult> Dashboard(string? city, string? fromCurrency, string? toCurrency, string? sourceTime, string? targetTime)
        {
            var userId = GetUserId();
            var user = _userService.GetUser(userId);
            
            // Apply User Preferences if params are missing
            if (user != null)
            {
                city ??= user.Preferences.DefaultCity;
                fromCurrency ??= user.Preferences.DefaultFromCurrency;
                toCurrency ??= user.Preferences.DefaultToCurrency;
                sourceTime ??= user.Preferences.DefaultSourceTimeZone;
                targetTime ??= user.Preferences.DefaultTargetTimeZone;
            }

            // If still missing (new user or no prefs), try IP-based detection
            if (string.IsNullOrEmpty(city) || string.IsNullOrEmpty(fromCurrency) || string.IsNullOrEmpty(sourceTime))
            {
                var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "";
                if (Request.Headers.ContainsKey("X-Forwarded-For"))
                    ip = Request.Headers["X-Forwarded-For"].FirstOrDefault() ?? "";

                var detectedLocation = await _externalApiService.GetLocationFromIpAsync(ip);
                
                city ??= detectedLocation.City;
                fromCurrency ??= detectedLocation.Currency;
                sourceTime ??= detectedLocation.TimeZoneId;
            }

            // Final Fallbacks (if detection failed or returned blanks)
            city = string.IsNullOrEmpty(city) ? "" : city;
            fromCurrency = string.IsNullOrEmpty(fromCurrency) ? "USD" : fromCurrency;
            toCurrency ??= "EUR";
            sourceTime = string.IsNullOrEmpty(sourceTime) ? "UTC" : sourceTime;
            targetTime ??= "GMT Standard Time";

            var model = new ViewModels.DashboardViewModel
            {
                UserName = user?.Name ?? user?.Email.Split('@')[0] ?? "User",
                Preferences = user?.Preferences ?? new UserPreferences(),
                SelectedCity = city,
                FromCurrency = fromCurrency,
                ToCurrency = toCurrency,
                SourceTimeZone = sourceTime,
                TargetTimeZone = targetTime,
                AvailableCurrencies = new List<string> { "USD", "EUR", "GBP", "JPY", "AUD", "CAD", "CHF", "CNY", "HKD", "NZD", "SGD", "INR" }, 
                AvailableTimeZones = LoadTimeZones()
            };

            // Don't fetch data server-side - let client handle it for faster initial render
            // This prevents the dashboard from hanging if external APIs are slow
            model.Weather = new Services.WeatherData();
            model.Currency = new Services.CurrencyConversionData { From = fromCurrency, To = toCurrency, Rate = 0 };
            model.TimeConversion = new Services.TimeData();

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
        public async Task<IActionResult> ResolveCurrencyByLocation(string location)
        {
            var currencyCode = await _externalApiService.GetCurrencyFromLocationAsync(location);
            return Json(new { currencyCode });
        }

        [HttpGet]
        public async Task<IActionResult> GetCurrencyHistoryJson(string from, string to, int days = 7)
        {
            var data = await _externalApiService.GetCurrencyHistoryAsync(from, to, days);
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

        [HttpGet]
        public async Task<IActionResult> ResolveTimeZone(string location)
        {
            var timezoneId = await _externalApiService.GetTimeZoneByLocationAsync(location);
            return Json(new { timezoneId });
        }

        [HttpGet]
        public async Task<IActionResult> GetNewsJson(string location, string? category = null, string sortBy = "relevance")
        {
            var data = await _externalApiService.GetNewsAsync(location, category, sortBy);
            return Json(data);
        }

        [HttpGet]
        public async Task<IActionResult> GetNewsDetailJson(string url)
        {
            var content = await _externalApiService.GetNewsDetailAsync(url);
            return Json(new { content });
        }

        [HttpGet]
        public IActionResult NewsDetail(int index)
        {
            var cachedNews = _externalApiService.GetCachedNews();
            if (index < 0 || index >= cachedNews.Count)
            {
                return RedirectToAction("Dashboard");
            }

            var item = cachedNews[index];
            return View(item);
        }

        [HttpGet]
        public async Task<IActionResult> SearchCountries(string query)
        {
            var data = await _externalApiService.SearchCountriesAsync(query);
            return Json(data);
        }

        [HttpGet]
        public async Task<IActionResult> GetCountryDetails(string name)
        {
            var data = await _externalApiService.GetCountryDetailsAsync(name);
            return Json(data);
        }

        [HttpGet]
        public async Task<IActionResult> GetAvailableCountriesJson()
        {
            var data = await _externalApiService.GetAvailableCountriesAsync();
            return Json(data);
        }

        [HttpGet]
        public async Task<IActionResult> GetHolidaysJson(string countryCode, int year)
        {
            var data = await _externalApiService.GetPublicHolidaysAsync(countryCode, year);
            return Json(data);
        }

        [HttpGet]
        public async Task<IActionResult> GetEmergencyNumbersJson()
        {
            var data = await _externalApiService.GetAllEmergencyNumbersAsync();
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

        private List<ViewModels.TimeZoneOption> LoadTimeZones()
        {
            var path = Path.Combine(_webHostEnvironment.WebRootPath, "data", "timezones.json");
            if (!System.IO.File.Exists(path))
            {
                // Fallback if file missing
                return new List<ViewModels.TimeZoneOption>();
            }

            var json = System.IO.File.ReadAllText(path);
            var zones = System.Text.Json.JsonSerializer.Deserialize<List<TimeZoneEntry>>(json, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            return zones.Select(z => new ViewModels.TimeZoneOption
            {
                Id = z.Id, // Windows ID (value sent to server)
                Name = z.Iana, // "Asia/Kolkata" 
                Offset = $"UTC{(z.Offset >= 0 ? "+" : "")}{z.Offset}",
                Abbr = z.Abbr,
                // Searchable text: Name + IANA + SearchKeywords
                FullName = $"{z.DisplayName} {z.Iana} {z.SearchKeywords ?? ""} {z.Abbr}".ToLower() 
            })
            .OrderBy(z => z.Name)
            .ToList();
        }

        private class TimeZoneEntry
        {
            public string Id { get; set; } = "";
            public string Iana { get; set; } = "";
            public string DisplayName { get; set; } = "";
            public string? SearchKeywords { get; set; }
            public double Offset { get; set; }
            public string? Abbr { get; set; }
        }

        private static string GetLocationAliases(string timezoneId)
        {
            // Keeping this for potential legacy use or removed if fully replaced by JSON keywords
            // Map IANA timezone IDs to common search terms (cities, states, countries)
            var aliases = new Dictionary<string, string>
            {
                // India
                ["Asia/Kolkata"] = " india mumbai delhi bangalore chennai hyderabad pune kolkata calcutta indian ist",
                // ... (rest omitted for brevity)
            };
            return aliases.TryGetValue(timezoneId, out var alias) ? alias : "";
        }
    }
}
