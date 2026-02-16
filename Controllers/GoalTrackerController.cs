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

        [HttpGet]
        [Route("GetCalendarData")]
        public async Task<JsonResult> GetCalendarData()
        {
            var schedules = await _goalService.GetUserSchedulesAsync(GetUserId());
            return Json(new { success = true, data = schedules });
        }

        [HttpGet]
        [Route("GetGoalAnalytics")]
        public async Task<JsonResult> GetGoalAnalytics()
        {
            var analytics = await _goalService.GetGoalAnalyticsAsync(GetUserId());
            return Json(new { success = true, data = analytics });
        }

        [HttpGet]
        [Route("GetUserAchievements")]
        public async Task<JsonResult> GetUserAchievements()
        {
            var achievements = await _goalService.GetUserAchievementsAsync(GetUserId());
            return Json(new { success = true, data = achievements });
        }

        [HttpPost]
        [Route("AddScheduleEntry")]
        [ValidateAntiForgeryToken]
        public async Task<JsonResult> AddScheduleEntry([FromBody] GoalSchedule schedule)
        {
            try
            {
                var created = await _goalService.CreateScheduleAsync(schedule, GetUserId());
                return Json(new { success = true, data = created, message = "Session scheduled." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        [Route("DeleteScheduleEntry")]
        [ValidateAntiForgeryToken]
        public async Task<JsonResult> DeleteScheduleEntry(int id)
        {
            var result = await _goalService.DeleteScheduleAsync(id, GetUserId());
            return Json(new { success = result.Success, message = result.Message });
        }

        // Sub-task Management
        [HttpGet]
        [Route("GetSubTasks")]
        public async Task<JsonResult> GetSubTasks(int goalId)
        {
            var subTasks = await _goalService.GetSubTasksAsync(goalId, GetUserId());
            return Json(new { success = true, data = subTasks });
        }

        [HttpPost]
        [Route("AddSubTask")]
        [ValidateAntiForgeryToken]
        public async Task<JsonResult> AddSubTask(int goalId, string title)
        {
            try
            {
                var subTask = await _goalService.AddSubTaskAsync(goalId, title, GetUserId());
                return Json(new { success = true, data = subTask, message = "Sub-task added." });
            }
            catch
            {
                return Json(new { success = false, message = "Failed to add sub-task." });
            }
        }

        [HttpPost]
        [Route("ToggleSubTask")]
        [ValidateAntiForgeryToken]
        public async Task<JsonResult> ToggleSubTask(int id)
        {
            var result = await _goalService.ToggleSubTaskAsync(id, GetUserId());
            return Json(new { success = result, message = result ? "Sub-task updated." : "Sub-task not found." });
        }

        [HttpPost]
        [Route("DeleteSubTask")]
        [ValidateAntiForgeryToken]
        public async Task<JsonResult> DeleteSubTask(int id)
        {
            var result = await _goalService.DeleteSubTaskAsync(id, GetUserId());
            return Json(new { success = result, message = result ? "Sub-task deleted." : "Sub-task not found." });
        }

        [HttpGet]
        [Route("GetSuggestedSubTasks")]
        public async Task<JsonResult> GetSuggestedSubTasks(int goalId)
        {
            var suggestions = await _goalService.GetSuggestedSubTasksAsync(goalId, GetUserId());
            return Json(new { success = true, data = suggestions });
        }
    }
}
