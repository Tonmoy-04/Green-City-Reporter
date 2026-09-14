using System.ComponentModel.DataAnnotations;

namespace GreenCityReporter.Services.Payments;

public class PaymentOptions
{
    public bool DemoMode { get; set; }
    public bool Enabled { get; set; }
    public bool Sandbox { get; set; } = true;
    public string StoreId { get; set; } = "";
    public string StorePassword { get; set; } = "";
    public string PublicBaseUrl { get; set; } = "";
    // SSLCommerz requires customer contact metadata even for donors who omit contact details.
    // Use the organization's real contact details as the fallback; never invent donor information.
    public string ContactEmail { get; set; } = "";
    public string ContactPhone { get; set; } = "";
    public Dictionary<string, string> Channels { get; set; } = new()
    {
        ["Card"] = "visacard,mastercard,amexcard", ["bKash"] = "bkash", ["Nagad"] = "nagad", ["Rocket"] = "dbblmobilebanking"
    };
    public bool IsReady => !DemoMode && Enabled && !string.IsNullOrWhiteSpace(StoreId) && !string.IsNullOrWhiteSpace(StorePassword)
        && Uri.TryCreate(PublicBaseUrl, UriKind.Absolute, out var uri) && uri.Scheme == "https"
        && string.IsNullOrEmpty(uri.UserInfo) && string.IsNullOrEmpty(uri.Query) && string.IsNullOrEmpty(uri.Fragment)
        && new EmailAddressAttribute().IsValid(ContactEmail) && !string.IsNullOrWhiteSpace(ContactEmail)
        && System.Text.RegularExpressions.Regex.IsMatch(ContactPhone, @"^\+?[0-9]{10,15}$");
}

public class DonationEmailOptions
{
    public bool Enabled { get; set; }
    public string Host { get; set; } = "";
    public int Port { get; set; } = 587;
    public bool UseStartTls { get; set; } = true;
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
    public string FromAddress { get; set; } = "";
    public bool IsReady => Enabled && !string.IsNullOrWhiteSpace(Host) && Port > 0 && Port <= 65535
        && !string.IsNullOrWhiteSpace(FromAddress) && new EmailAddressAttribute().IsValid(FromAddress);
}
