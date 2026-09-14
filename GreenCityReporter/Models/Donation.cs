using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace GreenCityReporter.Models;

// Preserve the numeric values used by existing manual donations.
public enum DonationStatus { Pending = 0, Confirmed = 1, Rejected = 2, Failed = 3, Cancelled = 4, UnderReview = 5 }

public class Donation
{
    public int Id { get; set; }
    [MaxLength(450)] public string? UserId { get; set; }
    public ApplicationUser? User { get; set; }
    [MaxLength(50)] public string DonorName { get; set; } = "";
    [MaxLength(50)] public string? Email { get; set; }
    [MaxLength(20)] public string? Phone { get; set; }
    [Precision(18, 2)] public decimal Amount { get; set; }
    [MaxLength(10)] public string PaymentMethod { get; set; } = "";
    [MaxLength(11)] public string SenderPhone { get; set; } = "";
    [MaxLength(50)] public string TransactionId { get; set; } = "";
    [MaxLength(500)] public string? Message { get; set; }
    [MaxLength(20)] public string Provider { get; set; } = "Manual";
    public bool IsSandbox { get; set; }
    [MaxLength(50)] public string? GatewayStoreId { get; set; }
    [MaxLength(80)] public string? BankTransactionId { get; set; }
    [MaxLength(80)] public string? ValidationId { get; set; }
    [MaxLength(80)] public string? GatewayPaymentType { get; set; }
    [MaxLength(64)] public string? CheckoutKey { get; set; }
    [MaxLength(64)] public string? ReceiptToken { get; set; }
    [MaxLength(2048)] public string? CheckoutUrl { get; set; }
    public DonationStatus Status { get; set; } = DonationStatus.Pending;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? PaidAt { get; set; }
    public DateTime? ReviewedAt { get; set; }
    [MaxLength(450)] public string? ReviewedBy { get; set; }
    public DateTime? LastCheckedAt { get; set; }
    public DateTime? ReceiptEmailSentAt { get; set; }
    public int EmailAttempts { get; set; }
    public DateTime? EmailNextAttemptAt { get; set; }
    public DateTime? EmailLeaseUntil { get; set; }
}
