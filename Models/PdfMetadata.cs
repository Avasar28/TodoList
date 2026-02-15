namespace TodoListApp.Models
{
    public class PdfMetadata
    {
        public int PageCount { get; set; }
        public long FileSize { get; set; }
        public string Version { get; set; } = string.Empty;
        public bool IsEncrypted { get; set; }
        public string Dimensions { get; set; } = string.Empty; // e.g., "A4 (595x842)"
    }
}
