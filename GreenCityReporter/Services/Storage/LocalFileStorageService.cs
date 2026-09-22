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

        public async Task<string?> SaveProfilePictureAsync(string userId, IFormFile file, CancellationToken cancellationToken = default)
        {
            if (!IsValidProfilePicture(file) || !IsSafeUserId(userId))
            {
                return null;
            }

            try
            {
                var uploadFolder = GetProfileUploadFolder();
                Directory.CreateDirectory(uploadFolder);

                var extension = GetProfileExtension(file);
                var filePath = Path.Combine(uploadFolder, userId + extension);
                await using var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None);
                await file.CopyToAsync(stream, cancellationToken);
                return $"/uploads/profile-pictures/users/{userId}{extension}";
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not save profile picture to local storage.");
                return null;
            }
        }

        public Task DeleteProfilePictureAsync(string? storedPath, CancellationToken cancellationToken = default)
        {
            const string prefix = "/uploads/profile-pictures/users/";
            if (string.IsNullOrWhiteSpace(storedPath) ||
                !storedPath.StartsWith(prefix, StringComparison.Ordinal) ||
                storedPath.Contains("..", StringComparison.Ordinal))
            {
                return Task.CompletedTask;
            }

            try
            {
                var fileName = Path.GetFileName(storedPath[prefix.Length..]);
                var filePath = Path.Combine(GetProfileUploadFolder(), fileName);
                if (string.Equals(Path.GetFileName(filePath), fileName, StringComparison.Ordinal) && File.Exists(filePath))
                {
                    File.Delete(filePath);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not delete local profile picture.");
            }

            return Task.CompletedTask;
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

        private string GetProfileUploadFolder()
        {
            var webRootPath = string.IsNullOrEmpty(_environment.WebRootPath)
                ? Path.Combine(_environment.ContentRootPath, "wwwroot")
                : _environment.WebRootPath;

            return Path.Combine(webRootPath, "uploads", "profile-pictures", "users");
        }

        private static bool IsValidProfilePicture(IFormFile file)
        {
            return file.Length > 0 &&
                   file.Length <= 5 * 1024 * 1024 &&
                   (string.Equals(file.ContentType, "image/jpeg", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(file.ContentType, "image/png", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(file.ContentType, "image/webp", StringComparison.OrdinalIgnoreCase));
        }

        private static string GetProfileExtension(IFormFile file)
        {
            return file.ContentType.ToLowerInvariant() switch
            {
                "image/png" => ".png",
                "image/webp" => ".webp",
                _ => ".jpg"
            };
        }

        private static bool IsSafeUserId(string userId)
        {
            return !string.IsNullOrWhiteSpace(userId) &&
                   string.Equals(Path.GetFileName(userId), userId, StringComparison.Ordinal) &&
                   !userId.Contains("..", StringComparison.Ordinal) &&
                   !userId.Contains('/', StringComparison.Ordinal) &&
                   !userId.Contains('\\', StringComparison.Ordinal);
        }
    }
}