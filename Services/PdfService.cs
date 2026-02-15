using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using PdfSharpCore.Drawing;
using PdfSharpCore.Pdf;
using PdfSharpCore.Pdf.IO;
using System.IO.Compression;
using TodoListApp.Data;
using TodoListApp.Models;

namespace TodoListApp.Services
{
    public class PdfService : IPdfService
    {
        private readonly IWebHostEnvironment _env;
        private readonly ApplicationDbContext _context;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private const long MaxFileSize = 20 * 1024 * 1024; // 20 MB

        public PdfService(IWebHostEnvironment env, ApplicationDbContext context, IHttpContextAccessor httpContextAccessor)
        {
            _env = env;
            _context = context;
            _httpContextAccessor = httpContextAccessor;
        }

        public async Task<string> MergePdfsAsync(List<IFormFile> files)
        {
            ValidateFiles(files, ".pdf");

            using var outputDocument = new PdfDocument();
            foreach (var file in files)
            {
                using var stream = file.OpenReadStream();
                using var inputDocument = PdfReader.Open(stream, PdfDocumentOpenMode.Import);
                for (int i = 0; i < inputDocument.PageCount; i++)
                {
                    outputDocument.AddPage(inputDocument.Pages[i]);
                }
            }

            var (relativePath, physicalPath) = GetStoragePaths("merged.pdf");
            outputDocument.Save(physicalPath);

            await SaveHistoryAsync("Merge", string.Join(", ", files.Select(f => f.FileName)), relativePath, new FileInfo(physicalPath).Length);
            
            return relativePath;
        }

        public async Task<string> SplitPdfAsync(IFormFile file, string pageRange, bool splitAll)
        {
            ValidateFiles(new List<IFormFile> { file }, ".pdf");

            using var stream = file.OpenReadStream();
            using var inputDocument = PdfReader.Open(stream, PdfDocumentOpenMode.Import);

            string relativePath, physicalPath;

            if (splitAll)
            {
                (relativePath, physicalPath) = GetStoragePaths("split_pages.zip");
                using (var archive = ZipFile.Open(physicalPath, ZipArchiveMode.Create))
                {
                    for (int i = 0; i < inputDocument.PageCount; i++)
                    {
                        using var pageDoc = new PdfDocument();
                        pageDoc.AddPage(inputDocument.Pages[i]);
                        
                        var tempPath = Path.GetTempFileName();
                        pageDoc.Save(tempPath);
                        archive.CreateEntryFromFile(tempPath, $"page_{i + 1}.pdf");
                        File.Delete(tempPath);
                    }
                }
            }
            else
            {
                (relativePath, physicalPath) = GetStoragePaths("split_selection.pdf");
                using var outputDocument = new PdfDocument();
                var pagesToExtract = ParsePageRange(pageRange, inputDocument.PageCount);
                
                foreach (var pageIndex in pagesToExtract)
                {
                    outputDocument.AddPage(inputDocument.Pages[pageIndex]);
                }
                outputDocument.Save(physicalPath);
            }

            await SaveHistoryAsync("Split", file.FileName, relativePath, new FileInfo(physicalPath).Length);
            return relativePath;
        }

        public async Task<string> ImagesToPdfAsync(List<IFormFile> images)
        {
            ValidateFiles(images, ".jpg", ".jpeg", ".png");

            using var outputDocument = new PdfDocument();

            foreach (var imageFile in images)
            {
                using var stream = imageFile.OpenReadStream();
                using var memStream = new MemoryStream();
                await stream.CopyToAsync(memStream);
                memStream.Position = 0;

                var page = outputDocument.AddPage();
                using var xImage = XImage.FromStream(() => new MemoryStream(memStream.ToArray()));
                using var gfx = XGraphics.FromPdfPage(page);
                
                double width = xImage.PixelWidth;
                double height = xImage.PixelHeight;
                double maxWidth = page.Width;
                double maxHeight = page.Height;
                
                double scale = Math.Min(maxWidth / width, maxHeight / height);
                if (scale < 1)
                {
                    width *= scale;
                    height *= scale;
                }
                
                gfx.DrawImage(xImage, (page.Width - width) / 2, (page.Height - height) / 2, width, height);
            }

            var (relativePath, physicalPath) = GetStoragePaths("images.pdf");
            outputDocument.Save(physicalPath);

            await SaveHistoryAsync("ImageToPdf", string.Join(", ", images.Take(3).Select(f => f.FileName)) + (images.Count > 3 ? "..." : ""), relativePath, new FileInfo(physicalPath).Length);
            return relativePath;
        }

        public async Task<string> CompressPdfAsync(IFormFile file, string compressionLevel)
        {
            ValidateFiles(new List<IFormFile> { file }, ".pdf");

            using var stream = file.OpenReadStream();
            using var document = PdfReader.Open(stream, PdfDocumentOpenMode.Modify);

            document.Options.FlateEncodeMode = PdfFlateEncodeMode.BestCompression;
            document.Options.UseFlateDecoderForJpegImages = PdfUseFlateDecoderForJpegImages.Automatic;
            document.Options.NoCompression = false;
            document.Options.CompressContentStreams = true;

            var (relativePath, physicalPath) = GetStoragePaths("compressed.pdf");
            document.Save(physicalPath);
            
            await SaveHistoryAsync("Compress", file.FileName, relativePath, new FileInfo(physicalPath).Length);
            return relativePath;
        }

        private (string relative, string physical) GetStoragePaths(string suffix)
        {
            var fileName = $"{Guid.NewGuid()}_{suffix}";
            var relativePath = $"/converted/{fileName}";
            var physicalPath = Path.Combine(_env.WebRootPath, "converted", fileName);
            
            var directory = Path.GetDirectoryName(physicalPath);
            if (!Directory.Exists(directory)) Directory.CreateDirectory(directory);

            return (relativePath, physicalPath);
        }

        private void ValidateFiles(List<IFormFile> files, params string[] allowedExtensions)
        {
            foreach (var file in files)
            {
                if (file.Length > MaxFileSize)
                    throw new Exception($"File {file.FileName} exceeds the 20MB limit.");

                var ext = Path.GetExtension(file.FileName).ToLower();
                if (!allowedExtensions.Contains(ext) && !allowedExtensions.Contains($".{ext}"))
                    throw new Exception($"File type {ext} is not allowed.");
            }
        }

        private async Task SaveHistoryAsync(string toolType, string originalNames, string storedPath, long size)
        {
            var userId = _httpContextAccessor.HttpContext?.User?.Identity?.Name ?? "Anonymous";
            
            var history = new PdfFileHistory
            {
                UserId = userId,
                ToolType = toolType,
                OriginalFileNames = originalNames,
                StoredFilePath = storedPath,
                FileSize = size
            };

            _context.PdfFiles.Add(history);
            await _context.SaveChangesAsync();
        }

        private List<int> ParsePageRange(string range, int maxPages)
        {
            var pages = new HashSet<int>();
            if (string.IsNullOrWhiteSpace(range)) return pages.ToList();

            var parts = range.Split(',');
            foreach (var part in parts)
            {
                if (part.Contains('-'))
                {
                    var ranges = part.Split('-');
                    if (ranges.Length == 2 && int.TryParse(ranges[0], out int start) && int.TryParse(ranges[1], out int end))
                    {
                        start = Math.Max(1, start);
                        end = Math.Min(maxPages, end);
                        for (int i = start; i <= end; i++) pages.Add(i - 1);
                    }
                }
                else
                {
                    if (int.TryParse(part, out int page) && page >= 1 && page <= maxPages)
                        pages.Add(page - 1);
                }
            }
            return pages.OrderBy(p => p).ToList();
        }
        public async Task<List<PdfFileHistory>> GetHistoryAsync(string userId, bool isAdmin)
        {
            if (isAdmin)
            {
                return await _context.PdfFiles
                    .OrderByDescending(h => h.CreatedAt)
                    .ToListAsync();
            }

            return await _context.PdfFiles
                .Where(h => h.UserId == userId)
                .OrderByDescending(h => h.CreatedAt)
                .ToListAsync();
        }

        public async Task<PdfMetadata> GetPdfMetadataAsync(IFormFile file)
        {
            ValidateFiles(new List<IFormFile> { file }, ".pdf");

            var metadata = new PdfMetadata
            {
                FileSize = file.Length,
                PageCount = 0,
                IsEncrypted = false,
                Version = "Unknown",
                Dimensions = "Unknown"
            };

            try
            {
                using var stream = file.OpenReadStream();
                // Try open import first to read without modifying
                using var document = PdfReader.Open(stream, PdfDocumentOpenMode.Import);
                metadata.PageCount = document.PageCount;
                metadata.Version = (document.Version / 10.0).ToString("F1");
                
                if (document.PageCount > 0)
                {
                    var page = document.Pages[0];
                    metadata.Dimensions = $"{page.Width.Point:F0}x{page.Height.Point:F0} pt";
                }
            }
            catch (PdfReaderException)
            {
                // Likely password protected if valid PDF structure but can't be opened
                metadata.IsEncrypted = true;
            }
            catch(Exception)
            {
                // General error or invalid file, just return basic info
            }

            return await Task.FromResult(metadata);
        }

        public async Task<long> GetUserStorageUsageAsync(string userId)
        {
            if (string.IsNullOrEmpty(userId)) return 0;

            return await _context.PdfFiles
                .Where(h => h.UserId == userId)
                .SumAsync(h => h.FileSize);
        }

        public async Task<bool> DeleteHistoryAsync(List<Guid> ids, string userId, bool isAdmin)
        {
            if (ids == null || !ids.Any()) return false;

            var query = _context.PdfFiles.AsQueryable();

            if (!isAdmin)
            {
                query = query.Where(f => f.UserId == userId);
            }

            var recordsToDelete = await query.Where(f => ids.Contains(f.Id)).ToListAsync();

            if (!recordsToDelete.Any()) return false;

            foreach (var record in recordsToDelete)
            {
                if (!string.IsNullOrEmpty(record.StoredFilePath))
                {
                    var filePath = Path.Combine(_env.WebRootPath, record.StoredFilePath.TrimStart('/').Replace('/', '\\'));
                    if (File.Exists(filePath))
                    {
                        try
                        {
                            File.Delete(filePath);
                        }
                        catch
                        {
                            // ignore
                        }
                    }
                }
                
                _context.PdfFiles.Remove(record);
            }

            await _context.SaveChangesAsync();
            return true;
        }
    }
}
