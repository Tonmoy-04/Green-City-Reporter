using System.Globalization;
using System.Net;
using System.Net.Mail;
using GreenCityReporter.Data;
using GreenCityReporter.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace GreenCityReporter.Services.Payments;

public interface IDonationReceiptSender
{
    Task SendAsync(Donation donation, CancellationToken cancellationToken);
}

public class SmtpDonationReceiptSender(IOptions<DonationEmailOptions> emailOptions, IOptions<PaymentOptions> payments) : IDonationReceiptSender
{
    public async Task SendAsync(Donation donation, CancellationToken cancellationToken)
    {
        var settings = emailOptions.Value;
        using var smtp = new SmtpClient(settings.Host, settings.Port) { EnableSsl = settings.UseStartTls, UseDefaultCredentials = false };
        if (!string.IsNullOrEmpty(settings.Username)) smtp.Credentials = new NetworkCredential(settings.Username, settings.Password);
        using var message = new MailMessage(settings.FromAddress, donation.Email!);
        message.Subject = (donation.IsSandbox ? "[SANDBOX] " : "") + "Green City Reporter donation receipt";
        message.Body = $"Thank you, {donation.DonorName}.\n\nYour {(donation.IsSandbox ? "test " : "")}donation is confirmed.\nAmount: BDT {donation.Amount.ToString("0.00", CultureInfo.InvariantCulture)}\nMethod: {donation.PaymentMethod}\nTransaction: {donation.TransactionId}\nBank transaction: {donation.BankTransactionId}\nDate (UTC): {donation.PaidAt:yyyy-MM-dd HH:mm}\n\nReceipt: {payments.Value.PublicBaseUrl.TrimEnd('/')}/Donation/Receipt?token={donation.ReceiptToken}\n";
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));
        await smtp.SendMailAsync(message, timeout.Token);
    }
}

public class DonationReceiptEmailService(ApplicationDbContext db, IDonationReceiptSender sender,
    IOptions<DonationEmailOptions> emailOptions, IWebHostEnvironment environment, ILogger<DonationReceiptEmailService> logger)
{
    public async Task SendPendingAsync(CancellationToken cancellationToken)
    {
        var settings = emailOptions.Value;
        if (!settings.IsReady || (!environment.IsDevelopment() && !settings.UseStartTls)) return;
        var now = DateTime.UtcNow;
        var recipients = await db.Donations.AsNoTracking().Where(d => d.Status == DonationStatus.Confirmed && d.Provider == "SSLCommerz"
            && d.Email != null && d.ReceiptEmailSentAt == null && (d.EmailNextAttemptAt == null || d.EmailNextAttemptAt <= now)
            && (d.EmailLeaseUntil == null || d.EmailLeaseUntil < now)).OrderBy(d => d.Id).Take(10).ToListAsync(cancellationToken);
        foreach (var donation in recipients)
        {
            var lease = DateTime.UtcNow.AddMinutes(2);
            var claimed = await db.Donations.Where(d => d.Id == donation.Id && d.Status == DonationStatus.Confirmed && d.ReceiptEmailSentAt == null
                && (d.EmailNextAttemptAt == null || d.EmailNextAttemptAt <= now) && (d.EmailLeaseUntil == null || d.EmailLeaseUntil < now))
                .ExecuteUpdateAsync(update => update.SetProperty(d => d.EmailLeaseUntil, lease)
                    .SetProperty(d => d.EmailAttempts, d => d.EmailAttempts + 1), cancellationToken);
            if (claimed != 1) continue;
            try
            {
                await sender.SendAsync(donation, cancellationToken);
                await db.Donations.Where(d => d.Id == donation.Id && d.EmailLeaseUntil == lease).ExecuteUpdateAsync(update => update
                    .SetProperty(d => d.ReceiptEmailSentAt, DateTime.UtcNow).SetProperty(d => d.EmailLeaseUntil, (DateTime?)null), cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
            catch (Exception)
            {
                var retry = DateTime.UtcNow.AddMinutes(Math.Min(60, Math.Pow(2, Math.Min(donation.EmailAttempts + 1, 6))));
                await db.Donations.Where(d => d.Id == donation.Id && d.EmailLeaseUntil == lease).ExecuteUpdateAsync(update => update
                    .SetProperty(d => d.EmailLeaseUntil, (DateTime?)null).SetProperty(d => d.EmailNextAttemptAt, retry), cancellationToken);
                logger.LogWarning("Donation {DonationId} receipt email will retry.", donation.Id);
            }
        }
    }
}
