using Microsoft.EntityFrameworkCore;
using TodoListApp.Data;

namespace TodoListApp.Services
{
    public class PdfCleanupService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<PdfCleanupService> _logger;

        public PdfCleanupService(IServiceProvider serviceProvider, ILogger<PdfCleanupService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("PdfCleanupService running at: {time}", DateTimeOffset.Now);

                try
                {
                    await CleanupOldFilesAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred executing PdfCleanupService.");
                }

                // Run every 24 hours
                await Task.Delay(TimeSpan.FromHours(24), stoppingToken);
            }
        }

        private async Task CleanupOldFilesAsync(CancellationToken stoppingToken)
        {
            using (var scope = _serviceProvider.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var env = scope.ServiceProvider.GetRequiredService<IWebHostEnvironment>();

                // Find files older than 30 days
                var cutoffDate = DateTime.UtcNow.AddDays(-30);
                var oldRecords = await context.PdfFiles
                    .Where(f => f.CreatedAt < cutoffDate)
                    .ToListAsync(stoppingToken);

                foreach (var record in oldRecords)
                {
                    // 1. Delete physical file
                    if (!string.IsNullOrEmpty(record.StoredFilePath))
                    {
                        var filePath = Path.Combine(env.WebRootPath, record.StoredFilePath.TrimStart('/'));
                        if (File.Exists(filePath))
                        {
                            try
                            {
                                File.Delete(filePath);
                                _logger.LogInformation($"Deleted physical file: {filePath}");
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, $"Failed to delete physical file: {filePath}");
                            }
                        }
                    }

                    // 2. Remove from DB
                    context.PdfFiles.Remove(record);
                }

                if (oldRecords.Any())
                {
                    await context.SaveChangesAsync(stoppingToken);
                    _logger.LogInformation($"Cleaned up {oldRecords.Count} old PDF records.");
                }
            }
        }
    }
}
