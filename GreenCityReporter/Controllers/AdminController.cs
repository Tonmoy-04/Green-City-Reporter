using GreenCityReporter.Data;
using GreenCityReporter.Models;
using GreenCityReporter.Models.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace GreenCityReporter.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public AdminController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // GET: /Admin
        public async Task<IActionResult> Index(
            Priority? priority,
            ReportStatus? status,
            int? categoryId)
        {
            var reportsQuery = _context.Reports
                .Include(r => r.Category)
                .Include(r => r.User)
                .AsQueryable();

            if (priority.HasValue)
            {
                reportsQuery = reportsQuery.Where(r => r.Priority == priority.Value);
            }

            if (status.HasValue)
            {
                reportsQuery = reportsQuery.Where(r => r.CurrentStatus == status.Value);
            }

            if (categoryId.HasValue)
            {
                reportsQuery = reportsQuery.Where(r => r.CategoryId == categoryId.Value);
            }

            var reports = await reportsQuery
                .OrderByDescending(r => r.Priority)
                .ThenBy(r => r.CreatedAt)
                .ToListAsync();

            var categories = await _context.Categories
                .OrderBy(c => c.Name)
                .ToListAsync();

            ViewBag.Categories = new SelectList(categories, "Id", "Name", categoryId);
            ViewBag.SelectedPriority = priority;
            ViewBag.SelectedStatus = status;
            ViewBag.SelectedCategoryId = categoryId;

            return View(reports);
        }

        // GET: /Admin/Report/5
        [HttpGet]
        public async Task<IActionResult> Report(int id)
        {
            var report = await _context.Reports
                .Include(r => r.Category)
                .Include(r => r.AiSuggestedCategory)
                .Include(r => r.Department)
                .Include(r => r.User)
                .Include(r => r.Comments)
                    .ThenInclude(c => c.User)
                .Include(r => r.StatusHistories)
                    .ThenInclude(sh => sh.Updater)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (report == null)
            {
                return NotFound();
            }

            ViewBag.Categories = new SelectList(await _context.Categories.OrderBy(c => c.Name).ToListAsync(), "Id", "Name", report.CategoryId);
            ViewBag.Departments = new SelectList(await _context.Departments.OrderBy(d => d.Name).ToListAsync(), "Id", "Name", report.DepartmentId);

            return View(report);
        }

        // POST: /Admin/UpdateStatus
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateStatus(
            int reportId,
            Models.Enums.ReportStatus newStatus,
            Models.Enums.Priority priority,
            int categoryId,
            int? departmentId,
            string? remarks)
        {
            var report = await _context.Reports
                .FirstOrDefaultAsync(r => r.Id == reportId);

            if (report == null)
            {
                return NotFound();
            }

            var admin = await _userManager.GetUserAsync(User);

            if (admin == null)
            {
                return Challenge();
            }

            if (!Enum.IsDefined(newStatus))
            {
                return BadRequest("Please select a valid report status.");
            }

            if (!Enum.IsDefined(priority))
            {
                return BadRequest("Please select a valid report priority.");
            }

            var categoryExists = await _context.Categories.AnyAsync(c => c.Id == categoryId);
            if (!categoryExists)
            {
                return BadRequest("Please select a valid category.");
            }

            if (departmentId.HasValue && !await _context.Departments.AnyAsync(d => d.Id == departmentId.Value))
            {
                return BadRequest("Please select a valid department.");
            }

            if (newStatus == ReportStatus.Assigned && !departmentId.HasValue)
            {
                ModelState.AddModelError(nameof(departmentId), "Assigned reports require a department.");
            }

            if (departmentId.HasValue && newStatus == ReportStatus.Pending)
            {
                newStatus = ReportStatus.Assigned;
            }

            if (!ModelState.IsValid)
            {
                TempData["ErrorMessage"] = "Assigned reports require a department.";
                return RedirectToAction(nameof(Report), new { id = reportId });
            }

            var previousStatus = report.CurrentStatus;

            // Update report
            report.CurrentStatus = newStatus;
            report.Priority = priority;
            report.CategoryId = categoryId;
            report.CategorySource = report.AiSuggestedCategoryId == categoryId ? "AI" : "Manual";
            report.DepartmentId = departmentId;
            report.UpdatedAt = DateTime.UtcNow;

            // Create status history
            var history = new StatusHistory
            {
                ReportId = report.Id,
                UpdatedBy = admin.Id,
                PreviousStatus = previousStatus,
                NewStatus = newStatus,
                Remarks = remarks ?? string.Empty,
                UpdatedAt = DateTime.UtcNow
            };

            _context.StatusHistories.Add(history);

            // Notify report owner
            string readableStatus = newStatus.ToString();
            var notification = new Notification
            {
                UserId = report.UserId,
                ReportId = report.Id,
                Message = $"Your report '{report.Title}' ({report.TrackingNumber}) status was updated to {readableStatus}.",
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            };
            _context.Notifications.Add(notification);

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] =
                "Report status updated successfully.";

            return RedirectToAction(
                nameof(Report),
                new { id = reportId }
            );
        }
        // POST: /Admin/AddComment
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddComment(int reportId, string message)
        {
            var admin = await _userManager.GetUserAsync(User);

            if (admin == null)
            {
                return Challenge();
            }

            var report = await _context.Reports
                .FirstOrDefaultAsync(r => r.Id == reportId);

            if (report == null)
            {
                return NotFound();
            }

            if (string.IsNullOrWhiteSpace(message))
            {
                TempData["CommentError"] = "Comment cannot be empty.";

                return RedirectToAction(nameof(Report), new { id = reportId });
            }

            if (message.Length > 1000)
            {
                TempData["CommentError"] = "Comment cannot exceed 1000 characters.";

                return RedirectToAction(nameof(Report), new { id = reportId });
            }

            var comment = new Comment
            {
                ReportId = reportId,
                UserId = admin.Id,
                Message = message.Trim(),
                CreatedAt = DateTime.UtcNow
            };

            _context.Comments.Add(comment);

            // Notify report owner
            var adminCommentNotification = new Notification
            {
                UserId = report.UserId,
                ReportId = report.Id,
                Message = $"An admin commented on your report '{report.Title}' ({report.TrackingNumber}).",
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            };
            _context.Notifications.Add(adminCommentNotification);

            await _context.SaveChangesAsync();

            TempData["CommentSuccess"] = "Comment added successfully.";

            return RedirectToAction(nameof(Report), new { id = reportId });
        }
    }
}