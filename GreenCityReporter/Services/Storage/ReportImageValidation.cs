using Microsoft.AspNetCore.Http;

namespace GreenCityReporter.Services.Storage
{
    internal static class ReportImageValidation
    {
        public const long MaxFileSize = 5 * 1024 * 1024;

        private static readonly IReadOnlyDictionary<string, string> AllowedTypes =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [".jpg"] = "image/jpeg",
                [".jpeg"] = "image/jpeg",
                [".png"] = "image/png",
                [".gif"] = "image/gif",
                [".webp"] = "image/webp"
            };

        public static bool IsValid(IFormFile file)
        {
            if (file == null || file.Length <= 0 || file.Length > MaxFileSize)
            {
                return false;
            }

            var extension = Path.GetExtension(file.FileName);
            return AllowedTypes.TryGetValue(extension, out var contentType) &&
                   string.Equals(file.ContentType, contentType, StringComparison.OrdinalIgnoreCase);
        }

        public static string GetSafeExtension(IFormFile file)
        {
            return Path.GetExtension(file.FileName).ToLowerInvariant();
        }
    }
}