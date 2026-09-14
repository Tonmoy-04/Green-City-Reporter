using GreenCityReporter.Data;
using GreenCityReporter.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;

namespace GreenCityReporter.Services.Payments;

public class DonationPaymentService(ApplicationDbContext context, IDonationGateway gateway)
{
    public async Task<bool> ReconcileAsync(Donation donation, string? validationId, CancellationToken cancellationToken)
    {
        if (donation.Provider != "SSLCommerz" || donation.Status is DonationStatus.Confirmed or DonationStatus.Rejected or DonationStatus.UnderReview) return true;
        var payment = await gateway.CheckAsync(donation, validationId, cancellationToken);
        var now = DateTime.UtcNow;
        if (payment == null)
        {
            await context.Donations.Where(d => d.Id == donation.Id).ExecuteUpdateAsync(update => update.SetProperty(d => d.LastCheckedAt, now), cancellationToken);
            return false;
        }
        if (payment.TransactionId != donation.TransactionId || payment.Amount != donation.Amount || payment.Currency != "BDT"
            || payment.Status is not ("VALID" or "FAILED" or "CANCELLED")
            || (payment.Status == "VALID" && (string.IsNullOrEmpty(payment.ValidationId) || string.IsNullOrEmpty(payment.BankTransactionId))))
            throw new PaymentGatewayException("The transaction could not be verified.");
        var status = payment.Status == "VALID" ? (payment.NeedsReview ? DonationStatus.UnderReview : DonationStatus.Confirmed)
            : payment.Status == "CANCELLED" ? DonationStatus.Cancelled : DonationStatus.Failed;
        // Delayed failures and callbacks must never overwrite confirmed or reviewed payments.
        var candidates = context.Donations.Where(d => d.Id == donation.Id &&
            (d.Status == DonationStatus.Pending || d.Status == DonationStatus.Failed || d.Status == DonationStatus.Cancelled));
        if (payment.Status == "VALID")
        {
            try
            {
                // Confirmation and durable email eligibility become visible in the same database update.
                await candidates.ExecuteUpdateAsync(update => update.SetProperty(d => d.Status, status)
                    .SetProperty(d => d.ValidationId, payment.ValidationId).SetProperty(d => d.BankTransactionId, payment.BankTransactionId)
                    .SetProperty(d => d.GatewayPaymentType, payment.PaymentType).SetProperty(d => d.PaidAt, now)
                    .SetProperty(d => d.LastCheckedAt, now).SetProperty(d => d.CheckoutUrl, (string?)null), cancellationToken);
            }
            catch (SqlException ex) when (ex.Number is 2601 or 2627)
            { throw new PaymentGatewayException("This bank transaction has already been recorded."); }
        }
        else
            await candidates.ExecuteUpdateAsync(update => update.SetProperty(d => d.Status, status).SetProperty(d => d.LastCheckedAt, now)
                .SetProperty(d => d.CheckoutUrl, (string?)null), cancellationToken);
        return true;
    }
}