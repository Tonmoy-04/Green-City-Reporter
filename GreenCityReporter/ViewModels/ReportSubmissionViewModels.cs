using System.ComponentModel.DataAnnotations;
using GreenCityReporter.Models.Enums;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace GreenCityReporter.ViewModels
{
    public class ReportSubmissionViewModel
    {
        [Required]
        [StringLength(200)]
        public string Title { get; set; } = string.Empty;

        [Required]
        public string Description { get; set; } = string.Empty;

        [Required]
        public string Address { get; set; } = string.Empty;

        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
    }

    public class ReportReviewViewModel : ReportSubmissionViewModel
    {
        public string? ImagePath { get; set; }
        public string? AISummary { get; set; }
        public string? AICategoryName { get; set; }
        public int? AICategoryId { get; set; }
        public Priority Priority { get; set; }
        public bool RequiresManualCategory { get; set; }
        public int? SelectedCategoryId { get; set; }
        public IEnumerable<SelectListItem> Categories { get; set; } = Enumerable.Empty<SelectListItem>();
    }
}