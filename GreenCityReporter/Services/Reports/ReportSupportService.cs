using GreenCityReporter.Data;
using GreenCityReporter.Models;
using Microsoft.EntityFrameworkCore;

namespace GreenCityReporter.Services.Reports;

public enum SupportResult { Added, AlreadySupported, OwnReport, Closed, NotFound }

public sealed class ReportSupportService(ApplicationDbContext db)
{
    public async Task<SupportResult> AddAsync(int reportId, string userId, CancellationToken cancellationToken = default)
    {
        var report = await db.Reports.AsNoTracking().Where(r => r.Id == reportId)
            .Select(r => new { r.UserId, r.CurrentStatus }).SingleOrDefaultAsync(cancellationToken);
        if (report == null) return SupportResult.NotFound;
        if (report.UserId == userId) return SupportResult.OwnReport;
        if (await db.ReportSupports.AnyAsync(s => s.ReportId == reportId && s.UserId == userId, cancellationToken))
            return SupportResult.AlreadySupported;
        if (!DuplicateReportService.IsActive(report.CurrentStatus)) return SupportResult.Closed;

        var support = new ReportSupport { ReportId = reportId, UserId = userId };
        db.ReportSupports.Add(support);
        try { await db.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateException)
        {
            db.Entry(support).State = EntityState.Detached;
            // A simultaneous click may have won the unique (report, citizen) key.
            if (await db.ReportSupports.AnyAsync(s => s.ReportId == reportId && s.UserId == userId, cancellationToken))
                return SupportResult.AlreadySupported;
            throw;
        }
        return SupportResult.Added;
    }
}
