using GreenCityReporter.Data;
using GreenCityReporter.Models;
using GreenCityReporter.Models.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using GreenCityReporter.Services.AI;
using GreenCityReporter.Services.Assignment;
using GreenCityReporter.ViewModels;

namespace GreenCityReporter.Controllers
{
    [Authorize]
    public class ReportController : Controller
    {
        private const string ReviewAICategoryIdKey = "ReportReviewAICategoryId";
        private const string ReviewPriorityKey = "ReportReviewPriority";
        private const string ReviewSummaryKey = "ReportReviewSummary";
        private const string ReviewCriticalKey = "ReportReviewCritical";
        private const string ReviewConfidenceKey = "ReportReviewConfidence";

        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IWebHostEnvironment _environment;
                private readonly IAIService _aiService;
                private readonly ILogger<ReportController> _logger;
        public ReportController(
          ApplicationDbContext context,
          UserManager<ApplicationUser> userManager,
                    IWebHostEnvironment environment,
                    IAIService aiService,
                    IReportAssignmentService assignmentService,
                    ILogger<ReportController> logger)
        {
            _context = context;
            _userManager = userManager;
            _environment = environment;
            _aiService = aiService;
            _assignmentService = assignmentService;
            _logger = logger;
        }

        private readonly IReportAssignmentService _assignmentService;

        // GET: /Report/Create
        [HttpGet]
        public async Task<IActionResult> Create()
        {
            return View();
        }

        // POST: /Report/Review
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Review(
            ReportSubmissionViewModel model,
            IFormFile? image)
        {
            if (!model.Latitude.HasValue || !model.Longitude.HasValue ||
                model.Latitude.Value < 23.60 || model.Latitude.Value > 23.95 ||
                model.Longitude.Value < 90.25 || model.Longitude.Value > 90.55)
            {
                ModelState.AddModelError(string.Empty, "Please select a valid issue location inside Dhaka on the map.");
            }

            if (!ModelState.IsValid)
            {
                return View("Create", model);
            }

            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return Challenge();
            }

            var cancellationToken = HttpContext.RequestAborted;
            var categoriesForSubmission = await _context.Categories
                .AsNoTracking()
                .OrderBy(c => c.Name)
                .ToListAsync(cancellationToken);

            var classification = await _aiService.ClassifyReportAsync(
                model.Title,
                model.Description,
                categoriesForSubmission.Select(c => c.Name),
                cancellationToken);

            var aiCategory = categoriesForSubmission.FirstOrDefault(c =>
                string.Equals(
                    c.Name,
                    classification?.Category,
                    StringComparison.OrdinalIgnoreCase));

            var aiPriority = await _aiService.DetectPriorityAsync(
                model.Title,
                model.Description,
                cancellationToken);
            var priority = aiPriority ?? Priority.Low;

            var aiSummary = await _aiService.SummarizeReportAsync(
                model.Title,
                model.Description,
                cancellationToken);

            var summary = string.IsNullOrWhiteSpace(aiSummary)
                ? null
                : aiSummary.Length <= 1000
                    ? aiSummary
                    : aiSummary[..1000];

            var imagePath = await SaveImageAsync(image, cancellationToken);
            if (image != null && image.Length > 0 && imagePath == null)
            {
                return View("Create", model);
            }

            TempData[ReviewAICategoryIdKey] = aiCategory?.Id.ToString();
            TempData[ReviewPriorityKey] = priority.ToString();
            TempData[ReviewSummaryKey] = summary;
            TempData[ReviewCriticalKey] = classification?.IsCritical == true && classification.Confidence >= 0.7;
            TempData[ReviewConfidenceKey] = classification?.Confidence?.ToString(System.Globalization.CultureInfo.InvariantCulture);

            var reviewModel = new ReportReviewViewModel
            {
                Title = model.Title,
                Description = model.Description,
                Address = model.Address,
                Latitude = model.Latitude,
                Longitude = model.Longitude,
                ImagePath = imagePath,
                AISummary = summary,
                AICategoryId = aiCategory?.Id,
                AICategoryName = aiCategory?.Name,
                AIConfidence = classification?.Confidence,
                IsCritical = classification?.IsCritical == true && classification.Confidence >= 0.7,
                Priority = priority,
                RequiresManualCategory = aiCategory == null,
                Categories = ToCategorySelectList(categoriesForSubmission, aiCategory?.Id)
            };

            return View(reviewModel);
        }

        // POST: /Report/Confirm
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Confirm(ReportReviewViewModel model)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }

            var cancellationToken = HttpContext.RequestAborted;
            var categories = await _context.Categories
                .AsNoTracking()
                .OrderBy(c => c.Name)
                .ToListAsync(cancellationToken);

            var aiCategoryId = GetTempDataInt(ReviewAICategoryIdKey);
            var reviewedPriority = GetTempDataPriority();
            var reviewedSummary = TempData.Peek(ReviewSummaryKey) as string;
            var isCritical = bool.TryParse(TempData.Peek(ReviewCriticalKey)?.ToString(), out var critical) && critical;
            var aiConfidence = double.TryParse(TempData.Peek(ReviewConfidenceKey)?.ToString(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var confidence)
                ? confidence
                : (double?)null;
            var aiCategory = aiCategoryId.HasValue
                ? categories.FirstOrDefault(c => c.Id == aiCategoryId.Value)
                : null;

            var category = categories.FirstOrDefault(c => c.Id == model.SelectedCategoryId) ?? aiCategory;
            var categorySource = category?.Id == aiCategory?.Id ? "AI" : "Manual";
            if (category == null)
                ModelState.AddModelError(nameof(ReportReviewViewModel.SelectedCategoryId), "Please select a valid category.");

            if (!reviewedPriority.HasValue)
            {
                ModelState.AddModelError(string.Empty, "The review has expired. Please start again.");
            }

            if (!IsValidImagePath(model.ImagePath))
            {
                ModelState.AddModelError(string.Empty, "The uploaded image is invalid.");
            }

            if (!ModelState.IsValid)
            {
                model.AICategoryId = aiCategory?.Id;
                model.AICategoryName = aiCategory?.Name;
                model.AISummary = reviewedSummary;
                model.AIConfidence = aiConfidence;
                model.IsCritical = isCritical;
                model.Priority = reviewedPriority ?? Priority.Low;
                model.RequiresManualCategory = aiCategory == null;
                model.Categories = ToCategorySelectList(categories, model.SelectedCategoryId ?? aiCategory?.Id);

                return View("Review", model);
            }

            var report = new Report
            {
                Title = model.Title,
                Description = model.Description,
                Address = model.Address,
                Latitude = model.Latitude,
                Longitude = model.Longitude,
                ImagePath = model.ImagePath,
                AISummary = reviewedSummary,
                CategoryId = category!.Id,
                AiSuggestedCategoryId = aiCategory?.Id,
                AiConfidence = aiConfidence,
                IsCritical = isCritical,
                CategorySource = categorySource,
                Priority = reviewedPriority!.Value,
                UserId = user.Id,
                TrackingNumber = GenerateTrackingNumber(),
                CurrentStatus = ReportStatus.Pending,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.Reports.Add(report);
            await _assignmentService.ApplyInitialAssignmentAsync(report, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);

            return RedirectToAction(nameof(Details), new { id = report.Id });
        }

        private async Task<string?> SaveImageAsync(
            IFormFile? image,
            CancellationToken cancellationToken)
        {
            if (image == null || image.Length == 0)
            {
                return null;
            }

            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
            var extension = Path.GetExtension(image.FileName).ToLowerInvariant();

            if (!allowedExtensions.Contains(extension))
            {
                ModelState.AddModelError(
                    string.Empty,
                    "Only JPG, JPEG, PNG, GIF, and WEBP images are allowed.");
                return null;
            }

            const long maxFileSize = 5 * 1024 * 1024;
            if (image.Length > maxFileSize)
            {
                ModelState.AddModelError(
                    string.Empty,
                    "The image size cannot exceed 5 MB.");
                return null;
            }

            var uploadFolder = GetUploadFolder();
            Directory.CreateDirectory(uploadFolder);

            var fileName = $"{Guid.NewGuid()}{extension}";
            var filePath = Path.Combine(uploadFolder, fileName);

            await using var stream = new FileStream(filePath, FileMode.CreateNew);
            await image.CopyToAsync(stream, cancellationToken);

            return $"/uploads/reports/{fileName}";
        }

        private bool IsValidImagePath(string? imagePath)
        {
            if (string.IsNullOrWhiteSpace(imagePath))
            {
                return true;
            }

            const string prefix = "/uploads/reports/";
            if (!imagePath.StartsWith(prefix, StringComparison.Ordinal) ||
                imagePath.Contains("..", StringComparison.Ordinal))
            {
                return false;
            }

            var fileName = imagePath[prefix.Length..];
            var extension = Path.GetExtension(fileName).ToLowerInvariant();
            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };

            return Guid.TryParse(Path.GetFileNameWithoutExtension(fileName), out _) &&
                   string.Equals(Path.GetFileName(fileName), fileName, StringComparison.Ordinal) &&
                   allowedExtensions.Contains(extension) &&
                   System.IO.File.Exists(Path.Combine(GetUploadFolder(), fileName));
        }

        private string GetUploadFolder()
        {
            var webRootPath = string.IsNullOrEmpty(_environment.WebRootPath)
                ? Path.Combine(_environment.ContentRootPath, "wwwroot")
                : _environment.WebRootPath;

            return Path.Combine(webRootPath, "uploads", "reports");
        }

        private static IEnumerable<SelectListItem> ToCategorySelectList(
            IEnumerable<Category> categories,
            int? selectedCategoryId = null)
        {
            return categories.Select(category => new SelectListItem
            {
                Value = category.Id.ToString(),
                Text = category.Name,
                Selected = category.Id == selectedCategoryId
            });
        }

        private int? GetTempDataInt(string key)
        {
            return int.TryParse(TempData.Peek(key) as string, out var value)
                ? value
                : null;
        }

        private Priority? GetTempDataPriority()
        {
            return Enum.TryParse<Priority>(TempData.Peek(ReviewPriorityKey) as string, out var priority) &&
                   Enum.IsDefined(priority)
                ? priority
                : null;
        }



        // GET: /Report/Dashboard
        [HttpGet]
        public async Task<IActionResult> Dashboard()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }

            var userReports = await _context.Reports
                .Include(r => r.Category)
                .Where(r => r.UserId == user.Id)
                .ToListAsync();

            var userNotifications = await _context.Notifications
                .Include(n => n.Report)
                .Where(n => n.UserId == user.Id)
                .OrderByDescending(n => n.CreatedAt)
                .ToListAsync();

            var viewModel = new ViewModels.CitizenDashboardViewModel
            {
                TotalReports = userReports.Count,
                PendingCount = userReports.Count(r => r.CurrentStatus == Models.Enums.ReportStatus.Pending),
                AssignedCount = userReports.Count(r => r.CurrentStatus == Models.Enums.ReportStatus.Assigned),
                ResolvedCount = userReports.Count(r => r.CurrentStatus == Models.Enums.ReportStatus.Resolved),
                RejectedCount = userReports.Count(r => r.CurrentStatus == Models.Enums.ReportStatus.Rejected),
                RecentReports = userReports.OrderByDescending(r => r.CreatedAt).Take(5).ToList(),
                RecentNotifications = userNotifications.Take(5).ToList(),
                UnreadNotificationCount = userNotifications.Count(n => !n.IsRead),
                CategoryCounts = userReports
                    .GroupBy(r => r.Category?.Name ?? "Uncategorized")
                    .ToDictionary(g => g.Key, g => g.Count())
            };

            return View(viewModel);
        }

        // GET: /Report/MyReports
        [HttpGet]
        public async Task<IActionResult> MyReports()
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return Challenge();
            }

            var reports = await _context.Reports
                .Include(r => r.Category)
                .Where(r => r.UserId == user.Id)
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();

            return View(reports);
        }

        // GET: /Report/Details/5
        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return Challenge();
            }

            var report = await _context.Reports
                .Include(r => r.Category)
                .Include(r => r.Comments)
                    .ThenInclude(c => c.User)
                .Include(r => r.StatusHistories)
                    .ThenInclude(sh => sh.Updater)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (report == null)
            {
                return NotFound();
            }

            // Citizens can only view their own reports.
            // Admin access will be handled separately.
            if (report.UserId != user.Id && !User.IsInRole("Admin"))
            {
                return Forbid();
            }

            return View(report);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddComment(int reportId, string message)
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return Challenge();
            }

            // Find the report
            var report = await _context.Reports
                .FirstOrDefaultAsync(r => r.Id == reportId);

            if (report == null)
            {
                return NotFound();
            }

            // Citizens can only comment on their own reports.
            // Admins can comment on any report.
            if (report.UserId != user.Id && !User.IsInRole("Admin"))
            {
                return Forbid();
            }

            // Validate comment
            if (string.IsNullOrWhiteSpace(message))
            {
                TempData["CommentError"] = "Comment cannot be empty.";

                return RedirectToAction(nameof(Details), new { id = reportId });
            }

            if (message.Length > 1000)
            {
                TempData["CommentError"] = "Comment cannot exceed 1000 characters.";

                return RedirectToAction(nameof(Details), new { id = reportId });
            }

            var comment = new Comment
            {
                ReportId = reportId,
                UserId = user.Id,
                Message = message.Trim(),
                CreatedAt = DateTime.UtcNow
            };

            _context.Comments.Add(comment);

            await _context.SaveChangesAsync();

            TempData["CommentSuccess"] = "Comment added successfully.";

            return RedirectToAction(nameof(Details), new { id = reportId });
        }
        // GET: /Report/Track
        [HttpGet]
        public IActionResult Track()
        {
            return View();
        }

        // POST: /Report/Track
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Track(string trackingNumber)
        {
            if (string.IsNullOrWhiteSpace(trackingNumber))
            {
                TempData["TrackError"] = "Please enter a tracking number.";
                return View("Track", trackingNumber);
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }

            var report = await _context.Reports
                .FirstOrDefaultAsync(r => r.TrackingNumber == trackingNumber.Trim());

            if (report == null)
            {
                TempData["TrackError"] = "No report found with that tracking number.";
                return View("Track", trackingNumber);
            }

            // A citizen can only track their own reports (per security requirements)
            // Admins could track any report.
            if (report.UserId != user.Id && !User.IsInRole("Admin"))
            {
                TempData["TrackError"] = "Access denied. You can only track your own reports.";
                return View("Track", trackingNumber);
            }

            return RedirectToAction(nameof(Details), new { id = report.Id });
        }

        private static string GenerateTrackingNumber()
        {
            return $"GCR-{DateTime.UtcNow:yyyyMMddHHmmss}-{Random.Shared.Next(1000, 9999)}";
        }
    }
}