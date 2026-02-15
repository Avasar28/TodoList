using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using TodoListApp.Services;

namespace TodoListApp.Controllers
{
    [Authorize]
    public class PdfToolsController : Controller
    {
        private readonly IPdfService _pdfService;

        public PdfToolsController(IPdfService pdfService)
        {
            _pdfService = pdfService;
        }

        public async Task<IActionResult> Index()
        {
            var userId = User.Identity?.Name ?? string.Empty;
            var isAdmin = User.HasClaim(c => c.Type == "IsAdmin" && c.Value == "True") || User.IsInRole("Admin");

            var history = await _pdfService.GetHistoryAsync(userId, isAdmin);
            ViewBag.IsAdmin = isAdmin;
            
            return View(history);
        }

        [HttpPost]
        public async Task<IActionResult> MergePdf(List<IFormFile> files)
        {
            if (files == null || files.Count == 0)
                return Json(new { success = false, message = "No files selected" });

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
        public async Task<IActionResult> SplitPdf(IFormFile file, string pageRange, bool splitAll)
        {
            if (file == null)
                return Json(new { success = false, message = "No file selected" });

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
        public async Task<IActionResult> ImageToPdf(List<IFormFile> images)
        {
            if (images == null || images.Count == 0)
                return Json(new { success = false, message = "No images selected" });

            try
            {
                var outputPath = await _pdfService.ImagesToPdfAsync(images);

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
        public async Task<IActionResult> CompressPdf(IFormFile file, string compressionLevel)
        {
            if (file == null)
                return Json(new { success = false, message = "No file selected" });

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
