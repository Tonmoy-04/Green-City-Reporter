using GreenCityReporter.Data;
using GreenCityReporter.Models;
using GreenCityReporter.Models.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace GreenCityReporter.Services.Background
{
    public sealed class ReportMonitoringService : BackgroundService
    {
        private const string AdminRole = "Admin";
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ReportMonitoringOptions _options;
        private readonly ILogger<ReportMonitoringService> _logger;

        public ReportMonitoringService(
            IServiceScopeFactory scopeFactory,
            IOptions<ReportMonitoringOptions> options,
            ILogger<ReportMonitoringService> logger)
        {
            _scopeFactory = scopeFactory;
            _options = options.Value;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!_options.Enabled)
            {
                return;
            }

            var checkInterval = TimeSpan.FromMinutes(
                Math.Max(1, _options.CheckIntervalMinutes));

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await CheckForOverdueReportsAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "An error occurred while monitoring overdue reports.");
                }

                try
                {
                    await Task.Delay(checkInterval, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
            }
        }

        private async Task CheckForOverdueReportsAsync(CancellationToken cancellationToken)
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

            var utcNow = DateTime.UtcNow;
            var overdueCutoff = utcNow.AddHours(-Math.Max(0, _options.OverdueHours));
            var waitingStatuses = new[]
            {
                ReportStatus.Pending,
                ReportStatus.InReview,
                ReportStatus.Assigned,
                ReportStatus.InProgress
            };

            var overdueReports = await context.Reports
                .Where(report =>
                    waitingStatuses.Contains(report.CurrentStatus) &&
                    report.CreatedAt <= overdueCutoff)
                .ToListAsync(cancellationToken);

            if (overdueReports.Count == 0)
            {
                return;
            }

            var admins = await userManager.GetUsersInRoleAsync(AdminRole);
            var notificationsCreated = 0;
            var prioritiesChanged = 0;

            foreach (var report in overdueReports)
            {
                var escalationPriority = GetEscalationPriority(report.CreatedAt, utcNow);

                if (escalationPriority.HasValue && escalationPriority.Value > report.Priority)
                {
                    report.Priority = escalationPriority.Value;
                    report.UpdatedAt = utcNow;
                    prioritiesChanged++;

                    var escalationMessage = CreateEscalationMessage(
                        report.TrackingNumber,
                        report.CreatedAt,
                        utcNow,
                        report.Priority);

                    foreach (var admin in admins)
                    {
                        var alreadyNotified = await context.Notifications
                            .AsNoTracking()
                            .AnyAsync(notification =>
                                notification.UserId == admin.Id &&
                                notification.ReportId == report.Id &&
                                notification.Message == escalationMessage,
                                cancellationToken);

                        if (alreadyNotified)
                        {
                            continue;
                        }

                        context.Notifications.Add(new Notification
                        {
                            UserId = admin.Id,
                            ReportId = report.Id,
                            Message = escalationMessage,
                            IsRead = false,
                            CreatedAt = utcNow
                        });
                        notificationsCreated++;
                    }
                }

                var message = CreateOverdueMessage(report.TrackingNumber);

                foreach (var admin in admins)
                {
                    var alreadyNotified = await context.Notifications
                        .AsNoTracking()
                        .AnyAsync(notification =>
                            notification.UserId == admin.Id &&
                            notification.ReportId == report.Id &&
                            notification.Message == message,
                            cancellationToken);

                    if (alreadyNotified)
                    {
                        continue;
                    }

                    context.Notifications.Add(new Notification
                    {
                        UserId = admin.Id,
                        ReportId = report.Id,
                        Message = message,
                        IsRead = false,
                        CreatedAt = DateTime.UtcNow
                    });
                    notificationsCreated++;
                }
            }

            if (notificationsCreated > 0 || prioritiesChanged > 0)
            {
                await context.SaveChangesAsync(cancellationToken);

                if (notificationsCreated > 0)
                {
                    _logger.LogInformation(
                        "Created {NotificationCount} overdue report notification(s) for administrators.",
                        notificationsCreated);
                }

                if (prioritiesChanged > 0)
                {
                    _logger.LogInformation(
                        "Escalated priority for {ReportCount} overdue report(s).",
                        prioritiesChanged);
                }
            }
        }

        private static Priority? GetEscalationPriority(DateTime createdAt, DateTime utcNow)
        {
            var age = utcNow - createdAt;

            if (age >= TimeSpan.FromHours(72))
            {
                return Priority.Critical;
            }

            if (age >= TimeSpan.FromHours(48))
            {
                return Priority.High;
            }

            if (age >= TimeSpan.FromHours(24))
            {
                return Priority.Medium;
            }

            return null;
        }

        private static string CreateEscalationMessage(
            string trackingNumber,
            DateTime createdAt,
            DateTime utcNow,
            Priority priority)
        {
            var overdueHours = (int)Math.Floor((utcNow - createdAt).TotalHours);

            return $"Report {trackingNumber} has been overdue for more than {overdueHours} hours and was escalated to {priority} priority. Immediate action is recommended.";
        }

        private string CreateOverdueMessage(string trackingNumber)
        {
            return $"Report {trackingNumber} has been waiting for admin action for more than {_options.OverdueHours} hours.";
        }
    }
}
