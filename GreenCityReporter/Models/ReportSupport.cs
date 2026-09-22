using System.ComponentModel.DataAnnotations;

namespace GreenCityReporter.Models;

public sealed class ReportSupport
{
    public int ReportId { get; set; }
    [MaxLength(450)] public string UserId { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Report Report { get; set; } = null!;
    public ApplicationUser User { get; set; } = null!;
}
