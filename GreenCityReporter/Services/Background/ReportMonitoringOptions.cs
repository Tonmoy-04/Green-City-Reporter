namespace GreenCityReporter.Services.Background
{
    public sealed class ReportMonitoringOptions
    {
        public bool Enabled { get; set; } = true;
        public int CheckIntervalMinutes { get; set; } = 60;
        public int OverdueHours { get; set; } = 24;
    }
}
