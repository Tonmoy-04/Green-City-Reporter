using GreenCityReporter.Models;

namespace GreenCityReporter.Services.Payments;

public record GatewayPayment(string TransactionId, string Status, decimal Amount, string Currency,
    string ValidationId, string BankTransactionId, string PaymentType, bool NeedsReview);

public interface IDonationGateway
{
    Task<string> CreateCheckoutAsync(Donation donation, CancellationToken cancellationToken);
    Task<GatewayPayment?> CheckAsync(Donation donation, string? validationId, CancellationToken cancellationToken);
}

public class PaymentGatewayException(string message) : Exception(message);
