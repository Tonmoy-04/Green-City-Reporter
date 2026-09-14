using GreenCityReporter.Data;
using GreenCityReporter.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace GreenCityReporter.Services.Payments;

public class DonationProcessingWorker(IServiceScopeFactory scopeFactory, IOptions<PaymentOptions> payments,
    ILogger<DonationProcessingWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await ProcessAsync(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception) { logger.LogWarning("Donation background processing will retry."); }
            try { if (!await timer.WaitForNextTickAsync(stoppingToken)) break; }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
        }
    }

    private async Task ProcessAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var now = DateTime.UtcNow;
        if (payments.Value.IsReady)
        {
            var pending = await db.Donations.AsNoTracking().Where(d => d.Provider == "SSLCommerz"
                && (d.Status == DonationStatus.Pending || d.Status == DonationStatus.Failed || d.Status == DonationStatus.Cancelled)
                && d.CreatedAt > now.AddDays(-7) && d.CreatedAt < now.AddMinutes(-1)
                && (d.LastCheckedAt == null || d.LastCheckedAt < now.AddMinutes(-5)))
                .OrderBy(d => d.LastCheckedAt).Take(5).ToListAsync(cancellationToken);
            foreach (var donation in pending)
            {
                try { await scope.ServiceProvider.GetRequiredService<DonationPaymentService>().ReconcileAsync(donation, null, cancellationToken); }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
                catch (Exception)
                {
                    await db.Donations.Where(d => d.Id == donation.Id).ExecuteUpdateAsync(update => update.SetProperty(d => d.LastCheckedAt, now), cancellationToken);
                    logger.LogWarning("Donation {DonationId} verification will retry.", donation.Id);
                }
            }
        }
        await scope.ServiceProvider.GetRequiredService<DonationReceiptEmailService>().SendPendingAsync(cancellationToken);
    }
}