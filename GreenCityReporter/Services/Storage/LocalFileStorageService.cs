using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace GreenCityReporter.Services.Storage
{
    public sealed class LocalFileStorageService : IFileStorageService
    {
        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<LocalFileStorageService> _logger;

        public LocalFileStorageService(
            IWebHostEnvironment environment,
            ILogger<LocalFileStorageService> logger)
        {
            _environment = environment;
            _logger = logger;
        }

        public async Task<string?> SaveReportImageAsync(IFormFile file, CancellationToken cancellationToken = default)
        {
            if (!ReportImageValidation.IsValid(file))
            {
                return null;
            }

            try
            {
                var uploadFolder = GetUploadFolder();
                Directory.CreateDirectory(uploadFolder);

                var fileName = $"{Guid.NewGuid():N}{ReportImageValidation.GetSafeExtension(file)}";
                var filePath = Path.Combine(uploadFolder, fileName);

                await using var stream = new FileStream(filePath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                await file.CopyToAsync(stream, cancellationToken);
                return $"/uploads/reports/{fileName}";
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not save report image to local storage.");
                return null;
            }
        }

        public Task DeleteReportImageAsync(string? storedPath, CancellationToken cancellationToken = default)
        {
            if (!IsValidReportImagePath(storedPath))
            {
                return Task.CompletedTask;
            }

            try
            {
                var fileName = Path.GetFileName(storedPath!["/uploads/reports/".Length..]);
                var filePath = Path.Combine(GetUploadFolder(), fileName);
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not delete local report image.");
            }

            return Task.CompletedTask;
        }

        public bool IsValidReportImagePath(string? storedPath)
        {
            if (string.IsNullOrWhiteSpace(storedPath) ||
                !storedPath.StartsWith("/uploads/reports/", StringComparison.Ordinal) ||
                storedPath.Contains("..", StringComparison.Ordinal))
            {
                return false;
            }

            var fileName = storedPath["/uploads/reports/".Length..];
            var extension = Path.GetExtension(fileName);
            return Guid.TryParse(Path.GetFileNameWithoutExtension(fileName), out _) &&
                   string.Equals(Path.GetFileName(fileName), fileName, StringComparison.Ordinal) &&
                   new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" }.Contains(extension, StringComparer.OrdinalIgnoreCase) &&
                   File.Exists(Path.Combine(GetUploadFolder(), fileName));
        }

        private string GetUploadFolder()
        {
            var webRootPath = string.IsNullOrEmpty(_environment.WebRootPath)
                ? Path.Combine(_environment.ContentRootPath, "wwwroot")
                : _environment.WebRootPath;

            return Path.Combine(webRootPath, "uploads", "reports");
        }
    }
}