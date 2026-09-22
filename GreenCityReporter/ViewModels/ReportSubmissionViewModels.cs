using System.ComponentModel.DataAnnotations;
using GreenCityReporter.Models.Enums;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

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

        [Required(ErrorMessage = "Please select an issue location on the map.")]
        [Range(23.60, 23.95, ErrorMessage = "Selected location latitude must be within Dhaka city boundary (23.60 to 23.95).")]
        public double? Latitude { get; set; }

        [Required(ErrorMessage = "Please select an issue location on the map.")]
        [Range(90.25, 90.55, ErrorMessage = "Selected location longitude must be within Dhaka city boundary (90.25 to 90.55).")]
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
        public bool SubmitAsSeparateReport { get; set; }
        [StringLength(16000)] public string? DuplicateReviewToken { get; set; }
        [BindNever, ValidateNever] public IReadOnlyList<DuplicateReportMatch> DuplicateMatches { get; set; } = [];
        [BindNever] public bool DuplicatesChecked { get; set; }
        [BindNever] public double DuplicateRadiusMeters { get; set; }
        [BindNever] public string? DuplicateCheckError { get; set; }
    }
}
