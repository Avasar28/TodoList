using System;
using System.ComponentModel.DataAnnotations;

namespace TodoListApp.Models
{
    public class PdfFileHistory
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        public string UserId { get; set; } = string.Empty;

        public string ToolType { get; set; } = string.Empty;

        public string OriginalFileNames { get; set; } = string.Empty;

        public string StoredFilePath { get; set; } = string.Empty;

        public long FileSize { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public int DownloadCount { get; set; } = 0;
    }
}
