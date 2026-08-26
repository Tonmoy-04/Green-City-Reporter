using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GreenCityReporter.Models
{
    public class Notification
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string UserId { get; set; } = string.Empty;

        [Required]
        public int ReportId { get; set; }

        [Required]
        [StringLength(500)]
        public string Message { get; set; } = string.Empty;

        public bool IsRead { get; set; } = false;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation Properties
        [ForeignKey("UserId")]
        public ApplicationUser User { get; set; } = null!;

        [ForeignKey("ReportId")]
        public Report Report { get; set; } = null!;
    }
}
