using System.Globalization;
using System.Text.Json;
using GreenCityReporter.Models;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;

namespace GreenCityReporter.Services.Payments;

public class SslCommerzGateway(HttpClient client, IOptions<PaymentOptions> options) : IDonationGateway
{
    private PaymentOptions Settings => options.Value;
    private static string BaseUrl(bool sandbox) => sandbox ? "https://sandbox.sslcommerz.com/" : "https://securepay.sslcommerz.com/";
    private static string Text(JsonElement json, string name) => json.TryGetProperty(name, out var value) ? value.ToString() : "";
    private static decimal Amount(JsonElement json) => decimal.TryParse(Text(json, "amount"), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var amount) ? amount : -1;
    private void EnsureReady(Donation donation)
    {
        if (!Settings.IsReady || donation.GatewayStoreId != Settings.StoreId)
            throw new PaymentGatewayException("Payment processing is temporarily unavailable.");
    }

    public async Task<string> CreateCheckoutAsync(Donation donation, CancellationToken cancellationToken)
    {
        EnsureReady(donation);
        if (!Settings.Channels.TryGetValue(donation.PaymentMethod, out var channel) || string.IsNullOrWhiteSpace(channel))
            throw new PaymentGatewayException("This payment method is unavailable.");
        var callbackBase = Settings.PublicBaseUrl.TrimEnd('/') + "/Donation/";
        var callbackToken = "?token=" + donation.ReceiptToken;
        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["store_id"] = Settings.StoreId, ["store_passwd"] = Settings.StorePassword,
            ["total_amount"] = donation.Amount.ToString("0.00", CultureInfo.InvariantCulture), ["currency"] = "BDT",
            ["tran_id"] = donation.TransactionId,
            ["success_url"] = callbackBase + "Success" + callbackToken,
            ["fail_url"] = callbackBase + "Fail" + callbackToken,
            ["cancel_url"] = callbackBase + "Cancel" + callbackToken,
            ["ipn_url"] = Settings.PublicBaseUrl.TrimEnd('/') + "/Donation/Ipn",
            ["cus_name"] = donation.DonorName, ["cus_email"] = donation.Email ?? Settings.ContactEmail,
            ["cus_phone"] = donation.Phone ?? Settings.ContactPhone, ["cus_add1"] = "Dhaka", ["cus_city"] = "Dhaka",
            ["cus_country"] = "Bangladesh", ["shipping_method"] = "NO", ["num_of_item"] = "1",
            ["product_name"] = "Green City Reporter donation", ["product_category"] = "donation", ["product_profile"] = "non-physical-goods",
            ["multi_card_name"] = channel
        });
        using var response = await client.PostAsync(BaseUrl(donation.IsSandbox) + "gwprocess/v4/api.php", content, cancellationToken);
        response.EnsureSuccessStatusCode();
        using var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
        var root = json.RootElement;
        var url = Text(root, "GatewayPageURL");
        // Never redirect to a URL supplied by a user or an unexpected gateway host.
        if (Text(root, "status") != "SUCCESS" || !IsCheckoutUrl(url, donation.IsSandbox))
            throw new PaymentGatewayException("The payment provider could not start checkout. Please try again later.");
        // Do not silently substitute another wallet when the merchant has not enabled this one.
        if (donation.PaymentMethod != "Card")
        {
            if (!root.TryGetProperty("gw", out var gateways) || !Text(gateways, "mobilebanking").Split(',').Contains(channel, StringComparer.OrdinalIgnoreCase))
                throw new PaymentGatewayException("This wallet is not enabled by the payment provider for this store. Please choose another method.");
        }
        return url;
    }

    public static bool IsCheckoutUrl(string url, bool sandbox)
    {
        if (url.Length > 2048 || !Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != "https" || !string.IsNullOrEmpty(uri.UserInfo)) return false;
        return sandbox ? uri.Host is "sandbox.sslcommerz.com" or "sandbox-gw.sslcommerz.com" : uri.Host == "securepay.sslcommerz.com";
    }

    private async Task<JsonDocument> GetAsync(Donation donation, string path, Dictionary<string, string?> parameters, CancellationToken cancellationToken)
    {
        parameters["store_id"] = Settings.StoreId;
        parameters["store_passwd"] = Settings.StorePassword;
        parameters["format"] = "json";
        using var response = await client.GetAsync(QueryHelpers.AddQueryString(BaseUrl(donation.IsSandbox) + path, parameters), cancellationToken);
        response.EnsureSuccessStatusCode();
        return await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
    }

    private async Task<GatewayPayment> ValidateAsync(Donation donation, string validationId, CancellationToken cancellationToken)
    {
        if (validationId.Length is < 1 or > 80) throw new PaymentGatewayException("Invalid transaction verification.");
        using var json = await GetAsync(donation, "validator/api/validationserverAPI.php", new() { ["val_id"] = validationId }, cancellationToken);
        var data = json.RootElement;
        var storeId = Text(data, "store_id");
        if (Text(data, "status") is not ("VALID" or "VALIDATED") || Text(data, "tran_id") != donation.TransactionId
            || Amount(data) != donation.Amount || Text(data, "currency") != "BDT" || Text(data, "val_id") != validationId
            || (storeId.Length > 0 && storeId != donation.GatewayStoreId) || string.IsNullOrEmpty(Text(data, "bank_tran_id")))
            throw new PaymentGatewayException("The transaction could not be verified.");
        var bankId = Text(data, "bank_tran_id");
        var type = Text(data, "card_type");
        if (bankId.Length > 80 || type.Length > 80) throw new PaymentGatewayException("Invalid transaction verification.");
        return new(donation.TransactionId, "VALID", donation.Amount, "BDT", validationId, bankId, type, Text(data, "risk_level") != "0");
    }

    public async Task<GatewayPayment?> CheckAsync(Donation donation, string? validationId, CancellationToken cancellationToken)
    {
        EnsureReady(donation);
        if (!string.IsNullOrEmpty(validationId)) return await ValidateAsync(donation, validationId, cancellationToken);
        using var json = await GetAsync(donation, "validator/api/merchantTransIDvalidationAPI.php", new() { ["tran_id"] = donation.TransactionId }, cancellationToken);
        var root = json.RootElement;
        if (Text(root, "APIConnect") != "DONE") throw new PaymentGatewayException("The payment provider is unavailable.");
        if (!root.TryGetProperty("element", out var entries) || entries.ValueKind != JsonValueKind.Array) return null;
        var matches = entries.EnumerateArray().Where(item => Text(item, "tran_id") == donation.TransactionId).ToList();
        var success = matches.FirstOrDefault(item => Text(item, "status") is "VALID" or "VALIDATED");
        if (success.ValueKind != JsonValueKind.Undefined) return await ValidateAsync(donation, Text(success, "val_id"), cancellationToken);
        if (matches.Count == 0 || matches.Any(item => Text(item, "status") is not ("FAILED" or "CANCELLED"))) return null;
        var latest = matches.OrderByDescending(item => Text(item, "tran_date")).First();
        if (Amount(latest) != donation.Amount || Text(latest, "currency") != "BDT") throw new PaymentGatewayException("The transaction could not be verified.");
        return new(donation.TransactionId, Text(latest, "status"), donation.Amount, "BDT", "", "", "", false);
    }
}
