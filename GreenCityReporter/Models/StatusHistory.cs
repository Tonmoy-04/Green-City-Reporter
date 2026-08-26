using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using GreenCityReporter.Models.Enums;

namespace GreenCityReporter.Models
{
    public class StatusHistory
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int ReportId { get; set; }

        [Required]
        public string UpdatedBy { get; set; } = string.Empty;

        public ReportStatus PreviousStatus { get; set; }
        public ReportStatus NewStatus { get; set; }

        [StringLength(500)]
        public string Remarks { get; set; } = string.Empty;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Navigation Properties
        [ForeignKey("ReportId")]
        public Report Report { get; set; } = null!;

        [ForeignKey("UpdatedBy")]
        public ApplicationUser Updater { get; set; } = null!;
    }
}
