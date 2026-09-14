using System.ComponentModel.DataAnnotations;
using GreenCityReporter.Models;

namespace GreenCityReporter.ViewModels;

public class DonationSubmissionViewModel
{
    [Range(typeof(decimal), "50", "100000", ErrorMessage = "Enter an amount between Tk 50 and Tk 100,000.")]
    public decimal Amount { get; set; } = 500;
    [Required, RegularExpression("^(Card|bKash|Nagad|Rocket)$")]
    [Display(Name = "Payment method")]
    public string PaymentMethod { get; set; } = "Card";
    [Required, StringLength(50), Display(Name = "Your name")]
    public string DonorName { get; set; } = "";
    [EmailAddress, StringLength(50), Display(Name = "Email (optional)")]
    public string? Email { get; set; }
    [RegularExpression(@"^\+?[0-9]{10,15}$", ErrorMessage = "Enter a valid phone number."), StringLength(20), Display(Name = "Phone (optional)")]
    public string? Phone { get; set; }
    [StringLength(500), Display(Name = "Message (optional)")]
    public string? Message { get; set; }
    [Required] public string CheckoutToken { get; set; } = "";
}

public class DonationPageViewModel
{
    public DonationSubmissionViewModel Form { get; set; } = new();
    public bool GatewayReady { get; set; }
    public bool IsDemo { get; set; }
    public bool IsSandbox { get; set; }
    public bool EmailReady { get; set; }
    public List<string> EnabledMethods { get; set; } = [];
    public List<Donation> History { get; set; } = [];
}
