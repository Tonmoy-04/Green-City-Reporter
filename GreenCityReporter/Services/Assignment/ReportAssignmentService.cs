using GreenCityReporter.Data;
using GreenCityReporter.Models;
using GreenCityReporter.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace GreenCityReporter.Services.Assignment
{
    public class ReportAssignmentService : IReportAssignmentService
    {
        private readonly ApplicationDbContext _context;

        public ReportAssignmentService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task ApplyInitialAssignmentAsync(Report report, CancellationToken cancellationToken = default)
        {
            report.DepartmentId = null;
            report.CurrentStatus = ReportStatus.Pending;

            if (!report.IsCritical)
            {
                return;
            }

            var departmentId = await _context.Categories
                .Where(category => category.Id == report.CategoryId)
                .Select(category => category.DefaultDepartmentId)
                .SingleOrDefaultAsync(cancellationToken);

            if (departmentId.HasValue && await _context.Departments.AnyAsync(d => d.Id == departmentId.Value, cancellationToken))
            {
                report.DepartmentId = departmentId.Value;
                report.CurrentStatus = ReportStatus.Assigned;
            }
        }
    }
}