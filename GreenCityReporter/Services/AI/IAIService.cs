using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using GreenCityReporter.Models.Enums;

namespace GreenCityReporter.Services.AI
{
    public sealed record ReportClassificationResult(string? Category, bool IsCritical, double? Confidence);

    public interface IAIService
    {
        Task<ReportClassificationResult?> ClassifyReportAsync(
            string title,
            string description,
            IEnumerable<string> availableCategories,
            CancellationToken cancellationToken = default);

        Task<string?> CategorizeReportAsync(
            string title,
            string description,
            IEnumerable<string> availableCategories,
            CancellationToken cancellationToken = default);

        Task<Priority?> DetectPriorityAsync(
            string title,
            string description,
            CancellationToken cancellationToken = default);

        Task<string?> SummarizeReportAsync(
            string title,
            string description,
            CancellationToken cancellationToken = default);

        Task<string?> ChatAsync(
            string message,
            string? context = null,
            CancellationToken cancellationToken = default);

        IAsyncEnumerable<string> ChatStreamAsync(
            string message,
            string? context = null,
            CancellationToken cancellationToken = default);
    }
}
