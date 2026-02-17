using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TodoListApp.Helpers;

namespace TodoListApp.Controllers
{
    [Authorize]
    [RequirePasskey]
    public class HabitTrackerController : Controller
    {
        public IActionResult Index()
        {
            // Habit Tracker is primarily client-side with localStorage, 
            // but we use this controller to wrap access with the RequirePasskey filter.
            return View();
        }
    }
}
