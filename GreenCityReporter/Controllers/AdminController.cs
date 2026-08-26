using GreenCityReporter.Data;
using GreenCityReporter.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
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
        public async Task<IActionResult> Index()
        {
            var reports = await _context.Reports
                .Include(r => r.Category)
                .Include(r => r.User)
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();

            return View(reports);
        }

        // GET: /Admin/Report/5
        [HttpGet]
        public async Task<IActionResult> Report(int id)
        {
            var report = await _context.Reports
                .Include(r => r.Category)
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

            return View(report);
        }

        // POST: /Admin/UpdateStatus
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateStatus(
            int reportId,
            Models.Enums.ReportStatus newStatus,
            Models.Enums.Priority priority,
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

            var previousStatus = report.CurrentStatus;

            // Update report
            report.CurrentStatus = newStatus;
            report.Priority = priority;
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

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] =
                "Report status updated successfully.";

            return RedirectToAction(
                nameof(Report),
                new { id = reportId }
            );
        }
    }
}