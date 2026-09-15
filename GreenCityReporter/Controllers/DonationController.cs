using System.Security.Claims;
using System.Security.Cryptography;
using GreenCityReporter.Data;
using GreenCityReporter.Models;
using GreenCityReporter.Services.Payments;
using GreenCityReporter.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace GreenCityReporter.Controllers;

[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class DonationController(ApplicationDbContext context, IDonationGateway gateway, DonationPaymentService paymentService,
    IOptions<PaymentOptions> options, IOptions<DonationEmailOptions> emailOptions, IDataProtectionProvider protection,
    ILogger<DonationController> logger) : Controller
{
    private readonly IDataProtector protector = protection.CreateProtector("GreenCity.Donation.Checkout.v1");
    private string? UserId => User.FindFirstValue(ClaimTypes.NameIdentifier);
    private string NewCheckoutToken() => protector.Protect($"{Guid.NewGuid():N}|{DateTime.UtcNow.Ticks}|{UserId}");
    private async Task<DonationPageViewModel> Page(DonationSubmissionViewModel? form = null)
    {
        if (form == null)
        {
            var user = UserId == null ? null : await context.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == UserId);
            form = new() { CheckoutToken = NewCheckoutToken(), DonorName = user?.FullName ?? "", Email = user?.Email, Phone = user?.PhoneNumber };
        }
        return new()
        {
            Form = form, GatewayReady = options.Value.DemoMode || options.Value.IsReady, IsDemo = options.Value.DemoMode, IsSandbox = options.Value.Sandbox, EmailReady = !options.Value.DemoMode && emailOptions.Value.IsReady,
            EnabledMethods = options.Value.DemoMode ? ["Card", "bKash", "Nagad", "Rocket"] : options.Value.Channels.Where(p => !string.IsNullOrWhiteSpace(p.Value)).Select(p => p.Key).ToList(),
            History = UserId == null ? [] : await context.Donations.AsNoTracking().Where(d => d.UserId == UserId)
                .OrderByDescending(d => d.CreatedAt).Take(20).ToListAsync()
        };
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        // Administrators manage donations from the protected report; they do not use the public checkout page.
        if (User.IsInRole("Admin")) return RedirectToAction(nameof(Manage));
        return View(await Page());
    }

    [HttpPost, ValidateAntiForgeryToken, EnableRateLimiting("donation-checkout")]
    public async Task<IActionResult> Submit([Bind(Prefix = "Form")] DonationSubmissionViewModel form, CancellationToken cancellationToken)
    {
        if (User.IsInRole("Admin")) return RedirectToAction(nameof(Manage));
        form.DonorName = (form.DonorName ?? "").Trim();
        form.Email = string.IsNullOrWhiteSpace(form.Email) ? null : form.Email.Trim();
        form.Phone = string.IsNullOrWhiteSpace(form.Phone) ? null : form.Phone.Trim();
        if (string.IsNullOrWhiteSpace(form.DonorName)) ModelState.AddModelError("Form.DonorName", "Please enter your name.");
        if (!options.Value.DemoMode && !options.Value.IsReady) ModelState.AddModelError("", "Payments are temporarily unavailable. Please try again later.");
        if (!(new[] { "Card", "bKash", "Nagad", "Rocket" }).Contains(form.PaymentMethod) || (!options.Value.DemoMode && (!options.Value.Channels.TryGetValue(form.PaymentMethod ?? "", out var channel) || string.IsNullOrWhiteSpace(channel))))
            ModelState.AddModelError("Form.PaymentMethod", "This payment method is unavailable.");
        if (decimal.Round(form.Amount, 2) != form.Amount) ModelState.AddModelError("Form.Amount", "Use no more than two decimal places.");
        string key = "";
        try
        {
            var ticket = protector.Unprotect(form.CheckoutToken ?? "").Split('|');
            if (ticket.Length != 3 || !Guid.TryParseExact(ticket[0], "N", out _) || !long.TryParse(ticket[1], out var ticks)
                || ticks > DateTime.UtcNow.Ticks || ticks < DateTime.UtcNow.AddHours(-24).Ticks || ticket[2] != (UserId ?? ""))
                throw new CryptographicException();
            key = ticket[0];
        }
        catch (Exception ex) when (ex is CryptographicException or ArgumentException)
        { ModelState.AddModelError("", "Checkout expired. Reload this page to start again."); }
        if (!ModelState.IsValid) return View("Index", await Page(form));
        var existing = await context.Donations.AsNoTracking().SingleOrDefaultAsync(d => d.CheckoutKey == key, cancellationToken);
        if (existing != null) return Resume(existing, form);
        var donation = new Donation
        {
            UserId = UserId, DonorName = form.DonorName, Email = form.Email, Phone = form.Phone, Amount = form.Amount,
            PaymentMethod = form.PaymentMethod!, Message = form.Message, Provider = options.Value.DemoMode ? "Demo" : "SSLCommerz", IsSandbox = options.Value.DemoMode || options.Value.Sandbox,
            GatewayStoreId = options.Value.DemoMode ? null : options.Value.StoreId, TransactionId = "GCR" + Convert.ToHexString(RandomNumberGenerator.GetBytes(12)),
            ReceiptToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)), CheckoutKey = key
        };
        context.Donations.Add(donation);
        try { await context.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException sql && sql.Number is 2601 or 2627)
        {
            context.ChangeTracker.Clear();
            existing = await context.Donations.AsNoTracking().SingleOrDefaultAsync(d => d.CheckoutKey == key, cancellationToken);
            if (existing == null) throw;
            return Resume(existing, form);
        }
        if (donation.Provider == "Demo") return RedirectToAction(nameof(Demo), new { token = donation.ReceiptToken });
        try
        {
            if (donation.Provider == "Demo") return RedirectToAction(nameof(Demo), new { token = donation.ReceiptToken });
            var checkoutUrl = await gateway.CreateCheckoutAsync(donation, cancellationToken);
            await context.Donations.Where(d => d.Id == donation.Id && d.Status == DonationStatus.Pending)
                .ExecuteUpdateAsync(update => update.SetProperty(d => d.CheckoutUrl, checkoutUrl), cancellationToken);
            return Redirect(checkoutUrl);
        }
        catch (PaymentGatewayException ex)
        {
            await context.Donations.Where(d => d.Id == donation.Id && d.Status == DonationStatus.Pending)
                .ExecuteUpdateAsync(update => update.SetProperty(d => d.Status, DonationStatus.Failed), cancellationToken);
            TempData["DonationNotice"] = ex.Message;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException)
        {
            // A timeout may happen after the provider created the session. Keep the attempt pending and reconcile it.
            logger.LogWarning("Donation {DonationId} checkout could not be completed; verification will retry.", donation.Id);
            TempData["DonationNotice"] = "The payment provider could not be reached. Your payment status will be checked before confirmation.";
        }
        return RedirectToAction(nameof(Receipt), new { token = donation.ReceiptToken });
    }

    [HttpGet]
    public async Task<IActionResult> Demo(string token, CancellationToken cancellationToken)
    {
        Response.Headers["Referrer-Policy"] = "no-referrer";
        Response.Headers["X-Robots-Tag"] = "noindex, nofollow";
        if (!options.Value.DemoMode || token?.Length != 64) return NotFound();
        var donation = await context.Donations.AsNoTracking().SingleOrDefaultAsync(d => d.ReceiptToken == token && d.Provider == "Demo", cancellationToken);
        if (donation == null) return NotFound();
        if (donation.Status != DonationStatus.Pending) return RedirectToAction(nameof(Receipt), new { token });
        return View(donation);
    }

    [HttpPost, ValidateAntiForgeryToken, EnableRateLimiting("donation-checkout")]
    public async Task<IActionResult> CompleteDemo(string token, string outcome, CancellationToken cancellationToken)
    {
        if (!options.Value.DemoMode || token?.Length != 64) return NotFound();
        var status = outcome switch { "success" => DonationStatus.Confirmed, "failure" => DonationStatus.Failed,
            "cancel" => DonationStatus.Cancelled, _ => (DonationStatus?)null };
        if (status == null) return BadRequest();
        var donation = await context.Donations.AsNoTracking().SingleOrDefaultAsync(d => d.ReceiptToken == token && d.Provider == "Demo", cancellationToken);
        if (donation == null) return NotFound();
        await context.Donations.Where(d => d.Id == donation.Id && d.Provider == "Demo" && d.Status == DonationStatus.Pending)
            .ExecuteUpdateAsync(update => update.SetProperty(d => d.Status, status.Value)
                .SetProperty(d => d.PaidAt, status == DonationStatus.Confirmed ? DateTime.UtcNow : (DateTime?)null), cancellationToken);
        return RedirectToAction(nameof(Receipt), new { token });
    }
    private IActionResult Resume(Donation donation, DonationSubmissionViewModel form)
    {
        if (donation.UserId != UserId || donation.Amount != form.Amount || donation.PaymentMethod != form.PaymentMethod
            || donation.DonorName != form.DonorName || donation.Email != form.Email || donation.Phone != form.Phone)
            return BadRequest("This checkout has already been used. Start a new donation.");
        if (options.Value.DemoMode && donation.Provider == "Demo" && donation.Status == DonationStatus.Pending) return RedirectToAction(nameof(Demo), new { token = donation.ReceiptToken });
        if (donation.Status == DonationStatus.Pending && donation.CheckoutUrl != null && SslCommerzGateway.IsCheckoutUrl(donation.CheckoutUrl, donation.IsSandbox))
            return Redirect(donation.CheckoutUrl);
        return RedirectToAction(nameof(Receipt), new { token = donation.ReceiptToken });
    }

    [HttpPost, IgnoreAntiforgeryToken, RequestSizeLimit(16384)]
    public async Task<IActionResult> Return([FromQuery] string? token, [FromForm(Name = "tran_id")] string transactionId,
        [FromForm(Name = "val_id")] string? validationId, CancellationToken cancellationToken)
    {
        Response.Headers["Referrer-Policy"] = "no-referrer";
        if (string.IsNullOrEmpty(transactionId) || transactionId.Length > 50) return BadRequest();
        var donation = await context.Donations.AsNoTracking().SingleOrDefaultAsync(d => d.TransactionId == transactionId && d.Provider == "SSLCommerz", cancellationToken);
        if (donation == null) return NotFound();
        try { await paymentService.ReconcileAsync(donation, validationId, cancellationToken); }
        catch (PaymentGatewayException) { TempData["DonationNotice"] = "Payment verification is pending. We have not confirmed this donation."; }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException)
        { TempData["DonationNotice"] = "The payment provider is temporarily unavailable. Verification will retry."; }
        if (token?.Length == 64 && token == donation.ReceiptToken) return RedirectToAction(nameof(Receipt), new { token });
        // Never disclose a donor's receipt token through an unauthenticated forged callback.
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, IgnoreAntiforgeryToken, RequestSizeLimit(16384)]
    public async Task<IActionResult> Ipn([FromForm(Name = "tran_id")] string transactionId,
        [FromForm(Name = "val_id")] string? validationId, CancellationToken cancellationToken)
    {
        if (!options.Value.IsReady) return StatusCode(503);
        if (string.IsNullOrEmpty(transactionId) || transactionId.Length > 50 || validationId?.Length > 80) return BadRequest();
        var donation = await context.Donations.AsNoTracking().SingleOrDefaultAsync(d => d.TransactionId == transactionId && d.Provider == "SSLCommerz", cancellationToken);
        if (donation == null) return NotFound();
        try { await paymentService.ReconcileAsync(donation, validationId, cancellationToken); return Ok(); }
        catch (PaymentGatewayException) { return BadRequest("Transaction verification failed."); }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException)
        { return StatusCode(503); }
    }

    [HttpGet]
    public async Task<IActionResult> Receipt(string token, CancellationToken cancellationToken)
    {
        Response.Headers["Referrer-Policy"] = "no-referrer";
        Response.Headers["X-Robots-Tag"] = "noindex, nofollow";
        if (token?.Length != 64) return NotFound();
        var donation = await context.Donations.AsNoTracking().SingleOrDefaultAsync(d => d.ReceiptToken == token, cancellationToken);
        if (donation == null) return NotFound();
        ViewData["EmailReady"] = emailOptions.Value.IsReady;
        return View(donation);
    }

    [HttpPost, ValidateAntiForgeryToken, EnableRateLimiting("donation-checkout")]
    public async Task<IActionResult> Refresh(string token, CancellationToken cancellationToken)
    {
        if (token?.Length != 64) return NotFound();
        var donation = await context.Donations.AsNoTracking().SingleOrDefaultAsync(d => d.ReceiptToken == token, cancellationToken);
        if (donation == null) return NotFound();
        try { await paymentService.ReconcileAsync(donation, null, cancellationToken); }
        catch (Exception ex) when (ex is PaymentGatewayException or HttpRequestException or TaskCanceledException or System.Text.Json.JsonException)
        { TempData["DonationNotice"] = "Verification is temporarily unavailable. Please try again later."; }
        return RedirectToAction(nameof(Receipt), new { token });
    }

    [HttpGet, Authorize(Roles = "Admin")]
    public async Task<IActionResult> Manage() => View(await context.Donations.AsNoTracking().Include(d => d.User)
        .OrderBy(d => d.Status == DonationStatus.Pending || d.Status == DonationStatus.UnderReview ? 0 : 1)
        .ThenByDescending(d => d.CreatedAt).Take(200).ToListAsync());

    [HttpPost, Authorize(Roles = "Admin"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Review(int id, DonationStatus status, bool verified)
    {
        if (status != DonationStatus.Confirmed && status != DonationStatus.Rejected) return BadRequest();
        if (status == DonationStatus.Confirmed && !verified) return BadRequest("Confirm that the payment or risk review was completed.");
        var reviewer = UserId;
        if (reviewer == null) return Challenge();
        // Admins can review old manual donations and gateway-verified payments held for risk review.
        // They cannot mark an unverified online payment as paid.
        var changed = await context.Donations.Where(d => d.Id == id && ((d.Provider == "Manual" && d.Status == DonationStatus.Pending)
                || (d.Provider == "SSLCommerz" && d.Status == DonationStatus.UnderReview && d.ValidationId != null && d.BankTransactionId != null)))
            .ExecuteUpdateAsync(update => update.SetProperty(d => d.Status, status).SetProperty(d => d.ReviewedAt, DateTime.UtcNow)
                .SetProperty(d => d.ReviewedBy, reviewer));
        TempData["DonationNotice"] = changed == 1 ? "Donation review saved." : "This donation is not eligible for manual review.";
        return RedirectToAction(nameof(Manage));
    }
}
