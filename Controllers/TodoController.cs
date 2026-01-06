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

        public TodoController(ITodoService todoService)
        {
            _todoService = todoService;
        }

        public IActionResult Dashboard()
        {
            var userId = GetUserId();
            var items = _todoService.GetAll(userId);
            var today = DateTime.Today;

            var model = new ViewModels.DashboardViewModel
            {
                TotalTasks = items.Count(),
                CompletedTasks = items.Count(t => t.IsCompleted),
                PendingTasks = items.Count(t => !t.IsCompleted),
                OverdueTasks = items.Count(t => !t.IsCompleted && t.DueDate.HasValue && t.DueDate.Value.Date < today)
            };

            return View(model);
        }

        private Guid GetUserId()
        {
            var idClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (idClaim != null && Guid.TryParse(idClaim.Value, out var id))
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
