using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GreenCityReporter.Models
{
    public class Comment
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int ReportId { get; set; }

        [Required]
        public string UserId { get; set; } = string.Empty;

        [Required]
        public string Message { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation Properties
        [ForeignKey("ReportId")]
        public Report Report { get; set; } = null!;

        [ForeignKey("UserId")]
        public ApplicationUser User { get; set; } = null!;
    }
}
