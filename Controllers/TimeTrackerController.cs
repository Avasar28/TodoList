using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TodoListApp.Models;
using TodoListApp.Services;

namespace TodoListApp.Controllers
{
    [Authorize]
    public class TimeTrackerController : Controller
    {
        private readonly ITimeTrackerService _timeTrackerService;

        public TimeTrackerController(ITimeTrackerService timeTrackerService)
        {
            _timeTrackerService = timeTrackerService;
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

        public IActionResult Index()
        {
            return View(); // The view will fetch data via AJAX or we can pass initial data here if preferred. 
                           // Plan said AJAX for Add, but Index usually loads valid data. 
                           // Let's pass the initial list for today to be server-side rendered for speed, 
                           // OR just let the JS handle it.
                           // Given the simple requirement, let's stick to the plan: Index View + JS logic.
        }

        [HttpGet]
        public IActionResult GetEntries(string date)
        {
            if (!DateTime.TryParse(date, out var parsedDate))
            {
                parsedDate = DateTime.Today;
            }
            var entries = _timeTrackerService.GetEntries(GetUserId(), parsedDate);
            return Json(entries);
        }

        [HttpGet]
        public IActionResult GetWeeklyEntries(string startDate, string endDate)
        {
            if (!DateTime.TryParse(startDate, out var start) || !DateTime.TryParse(endDate, out var end))
            {
                return BadRequest("Invalid date range");
            }

            var entries = _timeTrackerService.GetEntriesRange(GetUserId(), start, end);
            return Json(entries);
        }

        [HttpPost]
        public IActionResult Add([FromBody] TimeTrackerEntry entry)
        {
            if (entry == null) return BadRequest();
            
            entry.UserId = GetUserId();
            // Ensure date is set correctly if not passed or ensure backend consistency
            if (entry.Date == default) entry.Date = DateTime.Today;

            _timeTrackerService.AddEntry(entry);
            return Json(new { success = true });
        }

        [HttpPost]
        public IActionResult Update([FromBody] TimeTrackerEntry entry)
        {
            if (entry == null) return BadRequest();
            entry.UserId = GetUserId();
            _timeTrackerService.UpdateEntry(entry);
            return Json(new { success = true });
        }

        [HttpPost]
        public IActionResult Delete(Guid id)
        {
            _timeTrackerService.DeleteEntry(id, GetUserId());
            return Json(new { success = true });
        }
    }
}
