using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using GreenCityReporter.Models.Enums;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace GreenCityReporter.Models
{
    public class Report
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(50)]
        public string TrackingNumber { get; set; } = string.Empty;

        [Required]
        public string UserId { get; set; } = string.Empty;

        [Required]
        public int CategoryId { get; set; }

        public int? AiSuggestedCategoryId { get; set; }

        public double? AiConfidence { get; set; }

        public bool IsCritical { get; set; }

        public int? DepartmentId { get; set; }

        [MaxLength(20)]
        public string CategorySource { get; set; } = "Manual";

        [Required]
        [StringLength(200)]
        public string Title { get; set; } = string.Empty;

        [Required]
        public string Description { get; set; } = string.Empty;

        [MaxLength(1000)]
        public string? AISummary { get; set; }

        [StringLength(255)]
        public string? ImagePath { get; set; }

        [Required]
        public string Address { get; set; } = string.Empty;

        public double? Latitude { get; set; }
        public double? Longitude { get; set; }

        public Priority Priority { get; set; } = Priority.Low;
        public ReportStatus CurrentStatus { get; set; } = ReportStatus.Pending;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Navigation Properties
        [ValidateNever]
        [ForeignKey("UserId")]
        public ApplicationUser User { get; set; } = null!;

        [ValidateNever]
        [ForeignKey("CategoryId")]
        public Category Category { get; set; } = null!;

        [ValidateNever]
        public Category? AiSuggestedCategory { get; set; }

        [ValidateNever]
        public Department? Department { get; set; }

        public ICollection<Comment> Comments { get; set; } = new List<Comment>();
        public ICollection<ReportSupport> Supports { get; set; } = new List<ReportSupport>();
        public ICollection<StatusHistory> StatusHistories { get; set; } = new List<StatusHistory>();
    }
}
