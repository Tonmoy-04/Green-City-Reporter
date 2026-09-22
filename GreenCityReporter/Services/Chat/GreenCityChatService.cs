using System.Security.Claims;
using GreenCityReporter.Data;
using GreenCityReporter.Models;
using GreenCityReporter.Services.AI;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace GreenCityReporter.Services.Chat
{
    public sealed class GreenCityChatService : IChatService
    {
        private readonly IAIService _aiService;
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public GreenCityChatService(
            IAIService aiService,
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager)
        {
            _aiService = aiService;
            _context = context;
            _userManager = userManager;
        }

        public async Task<string?> AskAsync(
            string message,
            ClaimsPrincipal user,
            CancellationToken cancellationToken = default)
        {
            var currentUser = await _userManager.GetUserAsync(user);

            if (currentUser == null)
            {
                return null;
            }

            var latestReport = await _context.Reports
                .AsNoTracking()
                .Where(report => report.UserId == currentUser.Id)
                .OrderByDescending(report => report.CreatedAt)
                .Select(report => new
                {
                    report.TrackingNumber,
                    report.Title,
                    Category = report.Category.Name,
                    report.CurrentStatus,
                    report.Priority,
                    report.CreatedAt
                })
                .FirstOrDefaultAsync(cancellationToken);

            var context = """
                You are the Green City Reporter assistant. Answer only questions about using Green City Reporter and the citizen's own report information. Do not invent features, statuses, categories, report data, or actions that are not described here.

                Green City Reporter facts:
                - Citizens can submit civic issue reports with a title, description, category, address, and optional image.
                - Citizens can track their reports from the Track page or their report dashboard.
                - Report statuses are Pending, Assigned, Resolved, and Rejected.
                - Reports may be categorized as available in the report form.
                - Administrators review reports and may update their status and priority.
                - Keep answers concise and practical.
                """;

            if (latestReport == null)
            {
                context += "\nThe authenticated citizen has not submitted any reports yet.";
            }
            else
            {
                context += $"""

                    The authenticated citizen's latest report is:
                    Tracking Number: {latestReport.TrackingNumber}
                    Title: {latestReport.Title}
                    Category: {latestReport.Category}
                    Status: {latestReport.CurrentStatus}
                    Priority: {latestReport.Priority}
                    Created: {latestReport.CreatedAt:yyyy-MM-dd}
                    """;
            }

            return await _aiService.ChatAsync(
                message.Trim(),
                context,
                cancellationToken);
        }

        public async IAsyncEnumerable<string> StreamAsync(
            string message,
            ClaimsPrincipal user,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var currentUser = await _userManager.GetUserAsync(user);
            if (currentUser == null) yield break;

            var latestReport = await _context.Reports.AsNoTracking()
                .Where(report => report.UserId == currentUser.Id)
                .OrderByDescending(report => report.CreatedAt)
                .Select(report => new { report.TrackingNumber, report.Title, Category = report.Category.Name, report.CurrentStatus, report.Priority, report.CreatedAt })
                .FirstOrDefaultAsync(cancellationToken);

            var context = "You are the Green City Reporter assistant. Answer only about using the app and the citizen's own reports. Keep answers concise. Reports can be submitted with title, description, category, address and optional image. Statuses: Pending, Assigned, Resolved, Rejected.";
            context += latestReport == null
                ? " The citizen has not submitted any reports yet."
                : $" Latest report: {latestReport.TrackingNumber}, {latestReport.Title}, {latestReport.Category}, status {latestReport.CurrentStatus}, priority {latestReport.Priority}, created {latestReport.CreatedAt:yyyy-MM-dd}.";

            await foreach (var chunk in _aiService.ChatStreamAsync(message.Trim(), context, cancellationToken))
                yield return chunk;
        }
    }
}
