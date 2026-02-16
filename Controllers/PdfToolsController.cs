using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using TodoListApp.Helpers;
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

        // Removed Index action - PDF Tools is now a dashboard widget only

        [HttpGet]
        public async Task<IActionResult> GetUserData()
        {
            var userId = User.Identity?.Name ?? string.Empty;
            var user = await _userManager.FindByNameAsync(userId);
            
            long usage = await _pdfService.GetUserStorageUsageAsync(userId);
            long limit = 500 * 1024 * 1024; // 500 MB
            
            return Json(new
            {
                storageUsage = usage,
                storageLimit = limit,
                favorites = user?.Preferences.FavoritePdfTools ?? new List<string>(),
                autoDeleteEnabled = user?.Preferences.AutoDeletePdfEnabled ?? false
            });
        }

        [HttpGet]
        public async Task<IActionResult> GetHistory()
        {
            var userId = User.Identity?.Name ?? string.Empty;
            var isAdmin = User.IsInRole("SuperAdmin") || User.IsInRole("Admin");
            var history = await _pdfService.GetHistoryAsync(userId, isAdmin);
            return Json(history);
        }

        [HttpPost]
        public async Task<IActionResult> ToggleFavorite([FromBody] ToggleFavoriteRequest request)
        {
            var userId = User.Identity?.Name ?? string.Empty;
            var user = await _userManager.FindByNameAsync(userId);
            
            if (user == null) return Json(new { success = false });

            var prefs = user.Preferences;
            if (prefs.FavoritePdfTools.Contains(request.ToolId))
            {
                prefs.FavoritePdfTools.Remove(request.ToolId);
            }
            else
            {
                prefs.FavoritePdfTools.Add(request.ToolId);
            }

            user.Preferences = prefs;
            await _userManager.UpdateAsync(user);
            return Json(new { success = true, favorites = user.Preferences.FavoritePdfTools });
        }

        [HttpPost]
        public async Task<IActionResult> ToggleAutoDelete([FromBody] ToggleAutoDeleteRequest request)
        {
            var userId = User.Identity?.Name ?? string.Empty;
            var user = await _userManager.FindByNameAsync(userId);
            
            if (user == null) return Json(new { success = false });

            var prefs = user.Preferences;
            prefs.AutoDeletePdfEnabled = request.Enabled;
            user.Preferences = prefs;
            await _userManager.UpdateAsync(user);
            
            return Json(new { success = true, enabled = user.Preferences.AutoDeletePdfEnabled });
        }

        [HttpDelete]
        [Route("PdfTools/DeleteHistory/{id}")]
        public async Task<IActionResult> DeleteHistory(string id)
        {
            var userId = User.Identity?.Name ?? string.Empty;
            var isAdmin = User.IsInRole("SuperAdmin") || User.IsInRole("Admin");

            if (!Guid.TryParse(id, out var guid))
                return Json(new { success = false, message = "Invalid ID" });

            var result = await _pdfService.DeleteHistoryAsync(new List<Guid> { guid }, userId, isAdmin);
            
            return Json(new { success = result, message = result ? "File deleted successfully" : "Failed to delete file" });
        }

        [HttpPost]
        public async Task<IActionResult> DeleteHistoryBulk([FromBody] List<Guid> ids)
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

    // Request models
    public class ToggleFavoriteRequest
    {
        public string ToolId { get; set; } = string.Empty;
    }

    public class ToggleAutoDeleteRequest
    {
        public bool Enabled { get; set; }
    }
}
