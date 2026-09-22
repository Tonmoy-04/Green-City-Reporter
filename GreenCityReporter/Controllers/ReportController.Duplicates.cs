using GreenCityReporter.Services.Reports;
using GreenCityReporter.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GreenCityReporter.Controllers;

public partial class ReportController
{
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CheckDuplicates(ReportReviewViewModel model)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Challenge();
        var cancellationToken = HttpContext.RequestAborted;
        var categories = await _context.Categories.AsNoTracking().OrderBy(c => c.Name).ToListAsync(cancellationToken);
        var aiCategory = categories.FirstOrDefault(c => c.Id == GetTempDataInt(ReviewAICategoryIdKey));
        var category = aiCategory ?? categories.FirstOrDefault(c => c.Id == model.SelectedCategoryId);
        model.AICategoryId = aiCategory?.Id;
        model.AICategoryName = aiCategory?.Name;
        model.AISummary = TempData.Peek(ReviewSummaryKey) as string;
        model.Priority = GetTempDataPriority() ?? Models.Enums.Priority.Low;
        model.RequiresManualCategory = aiCategory == null;
        model.Categories = ToCategorySelectList(categories, model.SelectedCategoryId);
        model.SubmitAsSeparateReport = false;
        ModelState.Remove(nameof(model.SubmitAsSeparateReport));

        if (!GetTempDataPriority().HasValue)
            model.DuplicateCheckError = "Your review has expired. Please start a new report.";
        else if (!ModelState.IsValid)
            model.DuplicateCheckError = "Please check your report details and location before checking nearby reports.";
        else
            await PopulateDuplicatesAsync(model, category?.Id, user.Id, cancellationToken);

        Response.Headers.CacheControl = "no-store";
        return Request.Headers["X-Requested-With"] == "XMLHttpRequest"
            ? PartialView("_DuplicateReports", model)
            : View("Review", model);
    }

    private async Task PopulateDuplicatesAsync(ReportReviewViewModel model, int? categoryId, string userId,
        CancellationToken cancellationToken)
    {
        model.DuplicateRadiusMeters = _duplicates.RadiusMeters;
        model.DuplicateReviewToken = null;
        ModelState.Remove(nameof(model.DuplicateReviewToken));
        if (!categoryId.HasValue || !model.Latitude.HasValue || !model.Longitude.HasValue ||
            !DuplicateReportService.IsValidLocation(model.Latitude.Value, model.Longitude.Value)) return;

        model.DuplicateMatches = await _duplicates.FindAsync(categoryId.Value, model.Latitude.Value,
            model.Longitude.Value, userId, cancellationToken);
        model.DuplicatesChecked = true;
        model.DuplicateReviewToken = _duplicateTokens.Create(userId, model, categoryId.Value, model.DuplicateMatches.Select(r => r.Id));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Support(int reportId, string? duplicateReviewToken)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Challenge();
        var ticket = _duplicateTokens.Read(duplicateReviewToken, user.Id);
        if (ticket == null || !ticket.ReportIds.Contains(reportId))
            return BadRequest("This suggestion has expired or is invalid. Return to your report review and check nearby reports again.");

        var cancellationToken = HttpContext.RequestAborted;
        var report = await _context.Reports.AsNoTracking().Where(r => r.Id == reportId)
            .Select(r => new { r.CategoryId, r.Latitude, r.Longitude }).SingleOrDefaultAsync(cancellationToken);
        if (report == null) return NotFound();
        if (report.CategoryId != ticket.CategoryId || !report.Latitude.HasValue || !report.Longitude.HasValue ||
            DuplicateReportService.DistanceMeters(ticket.Latitude, ticket.Longitude, report.Latitude.Value, report.Longitude.Value) > _duplicates.RadiusMeters)
            return BadRequest("This report no longer matches your location and category. Please check nearby reports again.");

        var result = await _supports.AddAsync(reportId, user.Id, cancellationToken);
        if (result == SupportResult.NotFound) return NotFound();
        if (result == SupportResult.Closed)
        {
            TempData["SupportError"] = "That issue has already been closed. You can submit a new report if the problem has returned.";
            return RedirectToAction(nameof(MyReports));
        }
        if (result == SupportResult.OwnReport) return RedirectToAction(nameof(Details), new { id = reportId });

        TempData["SupportSuccess"] = result == SupportResult.Added
            ? "Your support has been added. You will receive notifications when this issue's status changes."
            : "You already support this issue. Your support is counted once.";
        return RedirectToAction(nameof(SupportedReport), new { id = reportId });
    }

    private IQueryable<SupportedReportViewModel> GetSupportedReports(string userId) =>
        _context.Reports.AsNoTracking().Where(r => r.Supports.Any(s => s.UserId == userId))
            .Select(r => new SupportedReportViewModel
            {
                Id = r.Id, TrackingNumber = r.TrackingNumber, Title = r.Title, CategoryName = r.Category.Name,
                Status = r.CurrentStatus, UpdatedAt = r.UpdatedAt, SupportCount = r.Supports.Count
            });

    [HttpGet]
    public async Task<IActionResult> SupportedReport(int id)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Challenge();
        var cancellationToken = HttpContext.RequestAborted;
        var report = await GetSupportedReports(user.Id).SingleOrDefaultAsync(r => r.Id == id, cancellationToken);
        if (report == null) return NotFound();
        report.History = await _context.StatusHistories.AsNoTracking().Where(h => h.ReportId == id)
            .OrderByDescending(h => h.UpdatedAt).ThenByDescending(h => h.Id)
            .Select(h => new SupportedReportStatus(h.NewStatus, h.UpdatedAt)).ToListAsync(cancellationToken);
        Response.Headers.CacheControl = "no-store";
        return View(report);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> StopSupporting(int reportId)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Challenge();
        await _context.ReportSupports.Where(s => s.ReportId == reportId && s.UserId == user.Id)
            .ExecuteDeleteAsync(HttpContext.RequestAborted);
        TempData["SupportSuccess"] = "You have stopped supporting this issue and will no longer receive its status updates.";
        return RedirectToAction(nameof(MyReports));
    }
}
