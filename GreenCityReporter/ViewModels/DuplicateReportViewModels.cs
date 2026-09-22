using GreenCityReporter.Models;
using GreenCityReporter.Models.Enums;

namespace GreenCityReporter.ViewModels;

// Deliberately excludes reporter identity, address, photos, description and private comments.
public sealed class DuplicateReportMatch
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public ReportStatus Status { get; set; }
    public double DistanceMeters { get; set; }
    public DateTime CreatedAt { get; set; }
    public int SupportCount { get; set; }
    public bool IsOwnReport { get; set; }
    public bool AlreadySupported { get; set; }
    public string StatusLabel => ReportStatusLabels.Display(Status);
}

public sealed class SupportedReportViewModel
{
    public int Id { get; set; }
    public string TrackingNumber { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public ReportStatus Status { get; set; }
    public DateTime UpdatedAt { get; set; }
    public int SupportCount { get; set; }
    public List<SupportedReportStatus> History { get; set; } = [];
    public string StatusLabel => ReportStatusLabels.Display(Status);
}

public sealed record SupportedReportStatus(ReportStatus Status, DateTime UpdatedAt)
{
    public string StatusLabel => ReportStatusLabels.Display(Status);
}

public sealed class MyReportsViewModel
{
    public IReadOnlyList<Report> SubmittedReports { get; set; } = [];
    public IReadOnlyList<SupportedReportViewModel> SupportedReports { get; set; } = [];
}

public static class ReportStatusLabels
{
    public static string Display(ReportStatus status) => status.ToString();
}
