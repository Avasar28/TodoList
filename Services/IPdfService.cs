using Microsoft.AspNetCore.Http;
using System.Collections.Generic;
using System.Threading.Tasks;
using TodoListApp.Models;

namespace TodoListApp.Services
{
    public interface IPdfService
    {
        Task<string> MergePdfsAsync(List<IFormFile> files);
        Task<string> SplitPdfAsync(IFormFile file, string pageRange, bool splitAll);
        Task<string> ImagesToPdfAsync(List<IFormFile> images);
        Task<string> CompressPdfAsync(IFormFile file, string compressionLevel);
        Task<List<PdfFileHistory>> GetHistoryAsync(string userId, bool isAdmin);
    }
}
