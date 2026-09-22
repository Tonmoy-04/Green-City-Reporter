using GreenCityReporter.Models;

namespace GreenCityReporter.Services.Assignment
{
    public interface IReportAssignmentService
    {
        Task ApplyInitialAssignmentAsync(Report report, CancellationToken cancellationToken = default);
    }
}