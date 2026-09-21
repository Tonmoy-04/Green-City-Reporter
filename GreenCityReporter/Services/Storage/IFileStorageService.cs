using Microsoft.AspNetCore.Http;

namespace GreenCityReporter.Services.Storage
{
    public interface IFileStorageService
    {
        Task<string?> SaveReportImageAsync(
            IFormFile file,
            CancellationToken cancellationToken = default);

        Task DeleteReportImageAsync(
            string? storedPath,
            CancellationToken cancellationToken = default);

        bool IsValidReportImagePath(string? storedPath);
    }
}