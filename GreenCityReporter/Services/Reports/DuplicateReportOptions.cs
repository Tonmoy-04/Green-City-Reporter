namespace GreenCityReporter.Services.Reports;

public sealed class DuplicateReportOptions
{
    public double RadiusMeters { get; set; } = 150;
    public int MaxResults { get; set; } = 5;
}
