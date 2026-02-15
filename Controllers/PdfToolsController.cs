using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using TodoListApp.Models;
using TodoListApp.Services;

namespace TodoListApp.Controllers
{
    [Authorize]
    public class PdfToolsController : Controller
    {
        private readonly IPdfService _pdfService;
        private readonly UserManager<ApplicationUser> _userManager;

        public PdfToolsController(IPdfService pdfService, UserManager<ApplicationUser> userManager)
        {
            _pdfService = pdfService;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index()
        {
            var userId = User.Identity?.Name ?? string.Empty;
            var isAdmin = User.IsInRole("SuperAdmin") || User.IsInRole("Admin");

            var history = await _pdfService.GetHistoryAsync(userId, isAdmin);
            
            // Storage Usage
            long usage = await _pdfService.GetUserStorageUsageAsync(userId);
            ViewBag.StorageUsage = usage;
            ViewBag.StorageLimit = 500 * 1024 * 1024; // 500 MB
            ViewBag.IsAdmin = isAdmin;

            // Get User Preferences
            var user = await _userManager.FindByNameAsync(userId);
            if (user != null)
            {
                 ViewBag.Favorites = user.Preferences.FavoritePdfTools;
                 ViewBag.AutoDeleteEnabled = user.Preferences.AutoDeletePdfEnabled;
            }
            else
            {
                 ViewBag.Favorites = new List<string>();
                 ViewBag.AutoDeleteEnabled = false;
            }
            
            return View(history);
        }

        [HttpPost]
        public async Task<IActionResult> ToggleFavorite(string toolType)
        {
            var userId = User.Identity?.Name ?? string.Empty;
            var user = await _userManager.FindByNameAsync(userId);
            
            if (user == null) return Json(new { success = false });

            var prefs = user.Preferences;
            if (prefs.FavoritePdfTools.Contains(toolType))
            {
                prefs.FavoritePdfTools.Remove(toolType);
            }
            else
            {
                prefs.FavoritePdfTools.Add(toolType);
            }

            user.Preferences = prefs;
            await _userManager.UpdateAsync(user);
            return Json(new { success = true, favorites = user.Preferences.FavoritePdfTools });
        }

        [HttpPost]
        public async Task<IActionResult> ToggleAutoDelete(bool enabled)
        {
            var userId = User.Identity?.Name ?? string.Empty;
            var user = await _userManager.FindByNameAsync(userId);
            
            if (user == null) return Json(new { success = false });

            var prefs = user.Preferences;
            prefs.AutoDeletePdfEnabled = enabled;
            user.Preferences = prefs;
            await _userManager.UpdateAsync(user);
            
            return Json(new { success = true, enabled = user.Preferences.AutoDeletePdfEnabled });
        }

        [HttpPost]
        public async Task<IActionResult> DeleteHistory([FromBody] List<Guid> ids)
        {
            var userId = User.Identity?.Name ?? string.Empty;
            var isAdmin = User.IsInRole("SuperAdmin") || User.IsInRole("Admin");

            var result = await _pdfService.DeleteHistoryAsync(ids, userId, isAdmin);
            
            return Json(new { success = result, message = result ? "Files deleted successfully" : "Failed to delete files" });
        }

        [HttpPost]
        public async Task<IActionResult> AnalyzePdf(IFormFile file)
        {
            if (file == null) return Json(new { success = false, message = "No file selected" });

            try
            {
                var metadata = await _pdfService.GetPdfMetadataAsync(file);
                return Json(new { success = true, metadata });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpGet]
        public IActionResult DownloadFile(string path)
        {
            if (string.IsNullOrEmpty(path) || !path.StartsWith("/converted/"))
                return BadRequest("Invalid file path");

            // Normalize path for security (prevent directory traversal)
            var fileName = Path.GetFileName(path); 
            var physicalPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "converted", fileName);

            if (!System.IO.File.Exists(physicalPath))
                return NotFound();

            var stream = new FileStream(physicalPath, FileMode.Open, FileAccess.Read);
            return File(stream, "application/pdf", fileName); 
        }

        private async Task<(bool allowed, string message)> CheckStorageLimit(long incomingSize)
        {
             var userId = User.Identity?.Name ?? string.Empty;
             long usage = await _pdfService.GetUserStorageUsageAsync(userId);
             long limit = 500 * 1024 * 1024;

             if (usage + incomingSize > limit)
             {
                 return (false, "Storage limit (500MB) exceeded. Please remove some files.");
             }
             return (true, string.Empty);
        }

        [HttpPost]
        public async Task<IActionResult> MergePdf(List<IFormFile> files)
        {
            if (files == null || files.Count == 0)
                return Json(new { success = false, message = "No files selected" });

            long totalSize = files.Sum(f => f.Length);
            var (allowed, msg) = await CheckStorageLimit(totalSize);
            if (!allowed) return Json(new { success = false, message = msg });

            try
            {
                var outputPath = await _pdfService.MergePdfsAsync(files);
                
                return Json(new
                {
                    success = true,
                    fileUrl = outputPath,
                    message = "Files merged successfully"
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Error merging files: {ex.Message}" });
            }
        }

        [HttpPost]
        public async Task<IActionResult> SplitPdf(List<IFormFile> files, string pageRange, bool splitAll)
        {
            var file = files?.FirstOrDefault();
            if (file == null)
                return Json(new { success = false, message = "No file selected" });

            var (allowed, msg) = await CheckStorageLimit(file.Length);
            if (!allowed) return Json(new { success = false, message = msg });

            try
            {
                var outputPath = await _pdfService.SplitPdfAsync(file, pageRange, splitAll);

                return Json(new
                {
                    success = true,
                    fileUrl = outputPath,
                    message = "PDF split successfully"
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Error splitting PDF: {ex.Message}" });
            }
        }

        [HttpPost]
        public async Task<IActionResult> ImagesToPdf(List<IFormFile> files)
        {
            if (files == null || files.Count == 0)
                return Json(new { success = false, message = "No images selected" });

            long totalSize = files.Sum(f => f.Length);
            var (allowed, msg) = await CheckStorageLimit(totalSize);
            if (!allowed) return Json(new { success = false, message = msg });

            try
            {
                var outputPath = await _pdfService.ImagesToPdfAsync(files);

                return Json(new
                {
                    success = true,
                    fileUrl = outputPath,
                    message = "Images converted successfully"
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Error converting images: {ex.Message}" });
            }
        }

        [HttpPost]
        public async Task<IActionResult> CompressPdf(List<IFormFile> files, string compressionLevel)
        {
            var file = files?.FirstOrDefault();
            if (file == null)
                return Json(new { success = false, message = "No file selected" });

            // Compress might reduce size, but initial upload counts
            var (allowed, msg) = await CheckStorageLimit(file.Length);
            if (!allowed) return Json(new { success = false, message = msg });

            try
            {
                var outputPath = await _pdfService.CompressPdfAsync(file, compressionLevel);

                return Json(new
                {
                    success = true,
                    fileUrl = outputPath,
                    message = "PDF compressed successfully"
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Error compressing PDF: {ex.Message}" });
            }
        }
    }
}
