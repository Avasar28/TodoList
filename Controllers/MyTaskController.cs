using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TodoListApp.Helpers;
using TodoListApp.Models;
using TodoListApp.Services;

namespace TodoListApp.Controllers
{
    [Authorize]
    [RequirePasskey]
    public class MyTaskController : Controller
    {
        private readonly ITodoService _todoService;

        public MyTaskController(ITodoService todoService)
        {
            _todoService = todoService;
        }

        [AuthorizeFeature("Page_Tasks")]
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

        private string GetUserId()
        {
            var idClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (idClaim != null)
            {
                return idClaim.Value;
            }
            throw new Exception("User ID not found");
        }
    }
}
