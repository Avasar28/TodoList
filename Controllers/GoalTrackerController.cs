using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TodoListApp.Helpers;
using TodoListApp.Models;
using TodoListApp.Services;

namespace TodoListApp.Controllers
{
    [Authorize]
    [AuthorizeFeature("Widget_GoalTracker")]
    [Route("[controller]")]
    public class GoalTrackerController : Controller
    {
        private readonly IGoalTrackerService _goalService;

        public GoalTrackerController(IGoalTrackerService goalService)
        {
            _goalService = goalService;
        }

        private string GetUserId() => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

        [HttpGet]
        [Route("")]
        public IActionResult Index()
        {
            return View();
        }

        [HttpGet]
        [Route("GetGoals")]
        public async Task<JsonResult> GetGoals()
        {
            var goals = await _goalService.GetUserGoalsAsync(GetUserId());
            return Json(new { success = true, data = goals, message = "Goals retrieved successfully." });
        }

        [HttpPost]
        [Route("CreateGoal")]
        [ValidateAntiForgeryToken]
        public async Task<JsonResult> CreateGoal([FromBody] Goal goal)
        {
            if (ModelState.IsValid)
            {
                var createdGoal = await _goalService.CreateGoalAsync(goal, GetUserId());
                return Json(new { success = true, data = createdGoal, message = "Goal created." });
            }
            return Json(new { success = false, message = "Invalid data." });
        }

        [HttpPost]
        [Route("UpdateGoal")]
        [ValidateAntiForgeryToken]
        public async Task<JsonResult> UpdateGoal([FromBody] Goal goal)
        {
            if (ModelState.IsValid)
            {
                var result = await _goalService.UpdateGoalAsync(goal, GetUserId());
                return Json(new { success = result.Success, data = goal, message = result.Message });
            }
            return Json(new { success = false, message = "Invalid data." });
        }

        [HttpPost]
        [Route("UpdateProgress")]
        [ValidateAntiForgeryToken]
        public async Task<JsonResult> UpdateProgress(int id, decimal currentValue)
        {
            var result = await _goalService.UpdateProgressAsync(id, currentValue, GetUserId());
            return Json(new { success = result.Success, message = result.Message });
        }

        [HttpPost]
        [Route("DeleteGoal")]
        [ValidateAntiForgeryToken]
        public async Task<JsonResult> DeleteGoal(int id)
        {
            var result = await _goalService.DeleteGoalAsync(id, GetUserId());
            return Json(new { success = result.Success, message = result.Message });
        }
    }
}
