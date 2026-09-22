using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using GreenCityReporter.Controllers;
using GreenCityReporter.Data;
using GreenCityReporter.Models;
using GreenCityReporter.Services.Payments;
using GreenCityReporter.ViewModels;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

var settings = Options.Create(new PaymentOptions { Enabled = true, StoreId = "test-store", StorePassword = "test-only-secret",
    PublicBaseUrl = "https://donations.example.test", ContactEmail = "support@example.test", ContactPhone = "01700000000" });
var renderConfiguration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
{
    ["SSLCOMMERZ:StoreId"] = "render-store", ["SSLCOMMERZ:StorePassword"] = "render-secret",
    ["SSLCOMMERZ:IsSandbox"] = "true", ["SSLCOMMERZ:ContactEmail"] = "payments@example.test",
    ["SSLCOMMERZ:ContactPhone"] = "01700000000", ["APP_BASE_URL"] = "https://green-city-reporter.onrender.com/"
}).Build();
var renderOptions = new PaymentOptions();
PaymentOptionsSetup.Configure(renderOptions, renderConfiguration);
Check(renderOptions.IsReady && renderOptions.Enabled && renderOptions.Sandbox &&
    renderOptions.PublicBaseUrl == "https://green-city-reporter.onrender.com",
    "Render SSLCommerz environment variables create a ready sandbox configuration");
var donation = NewDonation();
var validation = new Dictionary<string, object> { ["status"] = "VALID", ["tran_id"] = donation.TransactionId, ["amount"] = "500.00",
    ["currency"] = "BDT", ["val_id"] = "validation-1", ["bank_tran_id"] = "bank-1", ["card_type"] = "VISA-Test", ["risk_level"] = "0", ["store_id"] = "test-store" };
var handler = new FakeHttpHandler(request => JsonSerializer.Serialize(validation));
var adapter = new SslCommerzGateway(new HttpClient(handler), settings);
Check((await adapter.CheckAsync(donation, "validation-1", default))?.Status == "VALID", "accepts provider-verified exact transaction");
foreach (var field in new[] { "tran_id", "amount", "currency", "val_id", "store_id", "status" })
{
    var previous = validation[field]; validation[field] = "INVALID";
    await Reject(() => adapter.CheckAsync(donation, "validation-1", default), "rejects mismatched " + field);
    validation[field] = previous;
}
validation["risk_level"] = "1";
Check((await adapter.CheckAsync(donation, "validation-1", default))!.NeedsReview, "holds payments flagged for risk review");
validation["risk_level"] = "0";
var initHandler = new FakeHttpHandler(request => {
    var payload = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
    Check(!payload.Contains("cvv", StringComparison.OrdinalIgnoreCase) && !payload.Contains("card_number", StringComparison.OrdinalIgnoreCase), "gateway initialization contains no card details");
    var fields = QueryHelpers.ParseQuery("?" + payload);
    Check(fields["success_url"].ToString().Contains("/Donation/Success?token=") &&
        fields["fail_url"].ToString().Contains("/Donation/Fail?token=") &&
        fields["cancel_url"].ToString().Contains("/Donation/Cancel?token=") &&
        fields["ipn_url"] == "https://donations.example.test/Donation/Ipn",
        "gateway initialization uses public success, failure, cancellation and IPN callbacks");
    return JsonSerializer.Serialize(new { status = "SUCCESS", GatewayPageURL = "https://sandbox.sslcommerz.com/pay/test", gw = new { mobilebanking = "bkash" } });
});
var initAdapter = new SslCommerzGateway(new HttpClient(initHandler), settings);
Check((await initAdapter.CreateCheckoutAsync(donation, default)).StartsWith("https://sandbox.sslcommerz.com/"), "creates secure hosted card checkout");
donation.PaymentMethod = "Nagad";
await Reject(() => initAdapter.CreateCheckoutAsync(donation, default), "does not substitute another wallet for unavailable Nagad");
Check(!SslCommerzGateway.IsCheckoutUrl("https://sandbox.sslcommerz.com.evil.test/pay", true), "rejects hostile gateway redirect hosts");
Check(!SslCommerzGateway.IsCheckoutUrl("http://sandbox.sslcommerz.com/pay", true), "requires HTTPS for payment redirects");
Check(!SslCommerzGateway.IsCheckoutUrl("https://securepay.sslcommerz.com/pay", true), "separates sandbox and live redirects");

using var connection = new SqliteConnection("Data Source=:memory:");
await connection.OpenAsync();
await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options);
await db.Database.EnsureCreatedAsync();
var fake = new FakeGateway();
var service = new DonationPaymentService(db, fake);
var record = NewDonation();
db.Donations.Add(record); await db.SaveChangesAsync();
fake.Payment = new(record.TransactionId, "VALID", 400, "BDT", "valid", "bank-safe", "VISA", false);
await Reject(() => service.ReconcileAsync(record, null, default), "status service rejects mismatched amount");
fake.Payment = new(record.TransactionId, "VALID", record.Amount, "BDT", "valid", "bank-safe", "VISA", false);
await service.ReconcileAsync(record, null, default);
await service.ReconcileAsync(record, null, default); // duplicate IPN with a stale snapshot
var confirmed = await db.Donations.AsNoTracking().SingleAsync(d => d.Id == record.Id);
Check(confirmed.Status == DonationStatus.Confirmed && confirmed.BankTransactionId == "bank-safe", "duplicate verification produces one confirmed donation");
fake.Payment = new(record.TransactionId, "FAILED", record.Amount, "BDT", "", "", "", false);
await service.ReconcileAsync(record, null, default);
Check((await db.Donations.AsNoTracking().SingleAsync(d => d.Id == record.Id)).Status == DonationStatus.Confirmed, "late failure cannot downgrade successful payment");
var risk = NewDonation(); db.Donations.Add(risk); await db.SaveChangesAsync();
fake.Payment = new(risk.TransactionId, "VALID", risk.Amount, "BDT", "risk-valid", "bank-risk", "bKash", true);
await service.ReconcileAsync(risk, null, default);
Check((await db.Donations.AsNoTracking().SingleAsync(d => d.Id == risk.Id)).Status == DonationStatus.UnderReview, "risky verified payment remains unconfirmed");
var duplicateBank = NewDonation(); db.Donations.Add(duplicateBank); await db.SaveChangesAsync();
fake.Payment = new(duplicateBank.TransactionId, "VALID", duplicateBank.Amount, "BDT", "other-valid", "bank-safe", "VISA", false);
try { await service.ReconcileAsync(duplicateBank, null, default); throw new Exception("Reused bank transaction accepted"); }
catch (SqliteException ex) when (ex.SqliteErrorCode == 19) { Console.WriteLine("PASS: bank transaction cannot confirm two donations"); }

foreach (var outcome in new[] { "FAILED", "CANCELLED" })
{
    var attempt = NewDonation(); attempt.Email = null; db.Donations.Add(attempt); await db.SaveChangesAsync();
    fake.Payment = new(attempt.TransactionId, outcome, attempt.Amount, "BDT", "", "", "", false);
    await service.ReconcileAsync(attempt, null, default);
    Check((await db.Donations.AsNoTracking().SingleAsync(d => d.Id == attempt.Id)).Status ==
        (outcome == "FAILED" ? DonationStatus.Failed : DonationStatus.Cancelled), "records verified " + outcome.ToLowerInvariant() + " outcome");
    fake.Payment = new(attempt.TransactionId, "VALID", attempt.Amount, "BDT", "delayed-valid-" + attempt.Id, "delayed-bank-" + attempt.Id, "VISA", false);
    await service.ReconcileAsync(await db.Donations.AsNoTracking().SingleAsync(d => d.Id == attempt.Id), null, default);
    Check((await db.Donations.AsNoTracking().SingleAsync(d => d.Id == attempt.Id)).Status == DonationStatus.Confirmed,
        "delayed success recovers earlier " + outcome.ToLowerInvariant() + " outcome");
}
var controller = new DonationController(db, fake, service, settings, Options.Create(new DonationEmailOptions()),
    new EphemeralDataProtectionProvider(), NullLogger<DonationController>.Instance)
{
    ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity()) } }
};
controller.TempData = new TempDataDictionary(controller.HttpContext, new MemoryTempData());
var page = (DonationPageViewModel)((ViewResult)await controller.Index()).Model!;
var form = page.Form; form.DonorName = "Guest Donor"; form.Email = null; form.Phone = null;
var first = await controller.Submit(form, default);
var second = await controller.Submit(form, default);
Check(first is RedirectResult && second is RedirectResult && fake.CreateCount == 1, "double submission resumes one gateway checkout");
form.Amount = 1000;
Check(await controller.Submit(form, default) is BadRequestObjectResult, "idempotency key cannot be reused for a different amount");
Check(await controller.Receipt(new string('0', 64), default) is NotFoundResult, "unguessable receipt tokens protect donor details");
foreach (var callback in new[] { (Name: "Success", GatewayStatus: "VALID", Expected: DonationStatus.Confirmed),
    (Name: "Fail", GatewayStatus: "FAILED", Expected: DonationStatus.Failed),
    (Name: "Cancel", GatewayStatus: "CANCELLED", Expected: DonationStatus.Cancelled) })
{
    var callbackDonation = NewDonation(); db.Donations.Add(callbackDonation); await db.SaveChangesAsync();
    fake.Payment = new(callbackDonation.TransactionId, callback.GatewayStatus, callbackDonation.Amount, "BDT",
        callback.GatewayStatus == "VALID" ? "callback-valid-" + callbackDonation.Id : "",
        callback.GatewayStatus == "VALID" ? "callback-bank-" + callbackDonation.Id : "", "VISA", false);
    var result = callback.Name switch
    {
        "Success" => await controller.Success(callbackDonation.ReceiptToken, callbackDonation.TransactionId, fake.Payment.ValidationId, default),
        "Fail" => await controller.Fail(callbackDonation.ReceiptToken, callbackDonation.TransactionId, null, default),
        _ => await controller.Cancel(callbackDonation.ReceiptToken, callbackDonation.TransactionId, null, default)
    };
    Check(result is RedirectToActionResult &&
        (await db.Donations.AsNoTracking().SingleAsync(d => d.Id == callbackDonation.Id)).Status == callback.Expected,
        callback.Name.ToLowerInvariant() + " callback stores only the validated gateway outcome");
}
controller.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, "admin-test") }, "test"));
await controller.Review(duplicateBank.Id, DonationStatus.Confirmed, true);
Check((await db.Donations.AsNoTracking().SingleAsync(d => d.Id == duplicateBank.Id)).Status == DonationStatus.Pending, "admin cannot confirm unverified gateway payment");

var emailSettings = Options.Create(new DonationEmailOptions { Enabled = true, Host = "test-only", FromAddress = "receipts@example.test" });
var sender = new FakeSender { Fail = true };
var emailService = new DonationReceiptEmailService(db, sender, emailSettings, new FakeEnvironment(), NullLogger<DonationReceiptEmailService>.Instance);
await emailService.SendPendingAsync(default);
var queued = await db.Donations.AsNoTracking().SingleAsync(d => d.Id == record.Id);
Check(queued.ReceiptEmailSentAt == null && queued.EmailNextAttemptAt != null && queued.EmailLeaseUntil == null, "SMTP failure leaves receipt queued for retry");
sender.Fail = false;
await db.Donations.Where(d => d.Id == record.Id).ExecuteUpdateAsync(u => u.SetProperty(d => d.EmailNextAttemptAt, (DateTime?)null));
await emailService.SendPendingAsync(default);
await emailService.SendPendingAsync(default);
Check(sender.SuccessCount == 1 && (await db.Donations.AsNoTracking().SingleAsync(d => d.Id == record.Id)).ReceiptEmailSentAt != null, "successful receipt delivery is recorded and not resent");
Check(!sender.SentIds.Contains(risk.Id), "unconfirmed risk payments do not receive success emails");
var demoSettings = Options.Create(new PaymentOptions { DemoMode = true });
var demoGateway = new FakeGateway();
var demoController = new DonationController(db, demoGateway, service, demoSettings, emailSettings,
    new EphemeralDataProtectionProvider(), NullLogger<DonationController>.Instance)
{ ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() } };
demoController.TempData = new TempDataDictionary(demoController.HttpContext, new MemoryTempData());
foreach (var method in new[] { "Card", "bKash", "Nagad", "Rocket" })
{
    var demoPage = (DonationPageViewModel)((ViewResult)await demoController.Index()).Model!;
    Check(demoPage.GatewayReady && demoPage.IsDemo && !demoPage.EmailReady, "demo checkout works without credentials and does not promise email");
    var demoForm = demoPage.Form; demoForm.DonorName = "Demo Student"; demoForm.PaymentMethod = method;
    var redirect = (RedirectToActionResult)await demoController.Submit(demoForm, default);
    var token = (string)redirect.RouteValues!["token"]!;
    Check(redirect.ActionName == "Demo", method + " opens local demo checkout");
    await demoController.CompleteDemo(token, "success", default);
    await demoController.CompleteDemo(token, "failure", default);
    var saved = await db.Donations.AsNoTracking().SingleAsync(d => d.ReceiptToken == token);
    Check(saved.Provider == "Demo" && saved.IsSandbox && saved.Status == DonationStatus.Confirmed && saved.BankTransactionId == null,
        method + " saves an immutable demo result without a bank transaction");
}
Check(demoGateway.CreateCount == 0, "demo never calls the payment gateway");
Check(await demoController.CompleteDemo(record.ReceiptToken!, "success", default) is NotFoundResult, "demo cannot change a real gateway record");
foreach (var outcome in new[] { "failure", "cancel" })
{
    var demoForm = ((DonationPageViewModel)((ViewResult)await demoController.Index()).Model!).Form;
    demoForm.DonorName = "Demo Student";
    var redirect = (RedirectToActionResult)await demoController.Submit(demoForm, default);
    var token = (string)redirect.RouteValues!["token"]!;
    await demoController.CompleteDemo(token, outcome, default);
    var saved = await db.Donations.AsNoTracking().SingleAsync(d => d.ReceiptToken == token);
    Check(saved.Status == (outcome == "failure" ? DonationStatus.Failed : DonationStatus.Cancelled), "demo simulates " + outcome);
}
demoSettings.Value.DemoMode = false;
Check(await demoController.Demo(new string('a', 64), default) is NotFoundResult, "demo endpoints are disabled when demo mode is off");
Console.WriteLine("All donation payment checks passed. No real gateway or SMTP requests were made.");
await DuplicateReportChecks.RunAsync();

static Donation NewDonation() => new() { DonorName = "Test Donor", Email = "donor@example.test", Amount = 500, PaymentMethod = "Card",
    Provider = "SSLCommerz", IsSandbox = true, GatewayStoreId = "test-store", TransactionId = "GCR" + Guid.NewGuid().ToString("N")[..24],
    ReceiptToken = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N") };
static void Check(bool condition, string name) { if (!condition) throw new Exception("FAIL: " + name); Console.WriteLine("PASS: " + name); }
static async Task Reject(Func<Task> action, string name) { try { await action(); } catch (PaymentGatewayException) { Console.WriteLine("PASS: " + name); return; } throw new Exception("FAIL: " + name); }
sealed class FakeHttpHandler(Func<HttpRequestMessage, string> response) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
        Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(response(request), Encoding.UTF8, "application/json") });
}
sealed class FakeGateway : IDonationGateway
{
    public GatewayPayment? Payment; public int CreateCount;
    public Task<string> CreateCheckoutAsync(Donation donation, CancellationToken cancellationToken) { CreateCount++; return Task.FromResult("https://sandbox.sslcommerz.com/pay/test"); }
    public Task<GatewayPayment?> CheckAsync(Donation donation, string? validationId, CancellationToken cancellationToken) => Task.FromResult(Payment);
}
sealed class MemoryTempData : ITempDataProvider
{
    public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
    public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
}
sealed class FakeSender : IDonationReceiptSender
{
    public bool Fail; public int SuccessCount; public HashSet<int> SentIds = [];
    public Task SendAsync(Donation donation, CancellationToken cancellationToken)
    { if (Fail) throw new IOException("Simulated SMTP outage"); SuccessCount++; SentIds.Add(donation.Id); return Task.CompletedTask; }
}
sealed class FakeEnvironment : IWebHostEnvironment
{
    public string EnvironmentName { get; set; } = "Development";
    public string ApplicationName { get; set; } = "Checks";
    public string ContentRootPath { get; set; } = "";
    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    public string WebRootPath { get; set; } = "";
    public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
}
