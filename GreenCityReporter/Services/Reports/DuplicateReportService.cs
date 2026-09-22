using GreenCityReporter.Data;
using GreenCityReporter.Models.Enums;
using GreenCityReporter.ViewModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace GreenCityReporter.Services.Reports;

public sealed class DuplicateReportService(ApplicationDbContext db, IOptions<DuplicateReportOptions> options)
{
    public double RadiusMeters => options.Value.RadiusMeters;
    public static bool IsActive(ReportStatus status) => status is ReportStatus.Pending or ReportStatus.Assigned;

    public async Task<IReadOnlyList<DuplicateReportMatch>> FindAsync(int categoryId, double latitude,
        double longitude, string userId, CancellationToken cancellationToken = default)
    {
        if (!IsValidLocation(latitude, longitude) || categoryId <= 0) return [];

        // The indexed bounding box limits SQL results; the spherical distance removes corner matches.
        var latitudeDelta = RadiusMeters / 110_000;
        var longitudeDelta = RadiusMeters / (110_000 * Math.Cos(latitude * Math.PI / 180));
        var candidates = await db.Reports.AsNoTracking()
            .Where(r => r.CategoryId == categoryId &&
                (r.CurrentStatus == ReportStatus.Pending || r.CurrentStatus == ReportStatus.Assigned) &&
                r.Latitude >= latitude - latitudeDelta && r.Latitude <= latitude + latitudeDelta &&
                r.Longitude >= longitude - longitudeDelta && r.Longitude <= longitude + longitudeDelta)
            .Select(r => new
            {
                r.Id, r.Title, CategoryName = r.Category.Name, r.CurrentStatus, r.CreatedAt,
                Latitude = r.Latitude!.Value, Longitude = r.Longitude!.Value,
                IsOwnReport = r.UserId == userId, SupportCount = r.Supports.Count,
                AlreadySupported = r.Supports.Any(s => s.UserId == userId)
            }).ToListAsync(cancellationToken);

        return candidates.Select(r => new DuplicateReportMatch
            {
                Id = r.Id, Title = r.Title, CategoryName = r.CategoryName, Status = r.CurrentStatus,
                CreatedAt = r.CreatedAt, DistanceMeters = DistanceMeters(latitude, longitude, r.Latitude, r.Longitude),
                IsOwnReport = r.IsOwnReport, SupportCount = r.SupportCount, AlreadySupported = r.AlreadySupported
            })
            .Where(r => r.DistanceMeters <= RadiusMeters)
            .OrderBy(r => r.DistanceMeters).ThenByDescending(r => r.CreatedAt).ThenBy(r => r.Id)
            .Take(options.Value.MaxResults).ToList();
    }

    public static bool IsValidLocation(double latitude, double longitude) =>
        double.IsFinite(latitude) && double.IsFinite(longitude) &&
        latitude is >= 23.60 and <= 23.95 && longitude is >= 90.25 and <= 90.55;

    public static double DistanceMeters(double latitude, double longitude, double otherLatitude, double otherLongitude)
    {
        const double radians = Math.PI / 180;
        var a = Math.Pow(Math.Sin((otherLatitude - latitude) * radians / 2), 2) +
            Math.Cos(latitude * radians) * Math.Cos(otherLatitude * radians) *
            Math.Pow(Math.Sin((otherLongitude - longitude) * radians / 2), 2);
        return 6_371_000 * 2 * Math.Asin(Math.Sqrt(Math.Clamp(a, 0, 1)));
    }
}
