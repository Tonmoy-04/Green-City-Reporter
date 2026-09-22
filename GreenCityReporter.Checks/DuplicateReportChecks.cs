using System.Security.Claims;
using System.Net;
using System.Text.RegularExpressions;
using System.Text.Json;
using GreenCityReporter.Controllers;
using GreenCityReporter.Data;
using GreenCityReporter.Models;
using GreenCityReporter.Models.Enums;
using GreenCityReporter.Services.AI;
using GreenCityReporter.Services.Reports;
using GreenCityReporter.ViewModels;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

internal static class DuplicateReportChecks
{
    public static async Task RunAsync()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<ApplicationDbContext>(o => o.UseSqlite(connection));
        services.AddIdentityCore<ApplicationUser>().AddRoles<IdentityRole>().AddEntityFrameworkStores<ApplicationDbContext>();
        await using var provider = services.BuildServiceProvider();
        var db = provider.GetRequiredService<ApplicationDbContext>();
        await db.Database.EnsureCreatedAsync();
        var userManager = provider.GetRequiredService<UserManager<ApplicationUser>>();
        db.Users.AddRange(new[] { "owner", "citizen", "other", "admin" }.Select(id => new ApplicationUser
        { Id = id, UserName = id, FullName = "Private " + id, Email = id + "@private.example.test" }));
        var road = new Category { Name = "Road Damage", Description = "Road repairs" };
        var waste = new Category { Name = "Waste Management", Description = "Waste clearance" };
        db.Categories.AddRange(road, waste);
        await db.SaveChangesAsync();
        var nearby = ReportAt("Broken road near the park", road.Id, 30);
        var old = ReportAt("পুরোনো রাস্তার গর্ত", road.Id, 70, ReportStatus.InProgress);
        old.CreatedAt = DateTime.UtcNow.AddYears(-1);
        var inside = ReportAt("Inside radius", road.Id, 149, ReportStatus.Assigned);
        var outside = ReportAt("Outside radius", road.Id, 151);
        var corner = ReportAt("Outside circle, inside bounding box", road.Id, 140);
        corner.Longitude += 140 / (111_195 * Math.Cos(23.8 * Math.PI / 180));
        var otherCategory = ReportAt("Garbage on the road", waste.Id, 10);
        var resolved = ReportAt("Resolved road issue", road.Id, 5, ReportStatus.Resolved);
        var rejected = ReportAt("Rejected road issue", road.Id, 5, ReportStatus.Rejected);
        var noPin = ReportAt("No coordinates", road.Id, 0); noPin.Latitude = null;
        db.Reports.AddRange(nearby, old, inside, outside, corner, otherCategory, resolved, rejected, noPin);
        await db.SaveChangesAsync();

        var settings = Options.Create(new DuplicateReportOptions());
        var detection = new DuplicateReportService(db, settings);
        var clock = new TestClock();
        var tokens = new DuplicateReviewTokens(new EphemeralDataProtectionProvider(), clock);
        var support = new ReportSupportService(db);
        var ai = new TestAI { Category = road.Name };

        var matches = await detection.FindAsync(road.Id, 23.8, 90.4, "citizen");
        Check(matches.Select(m => m.Id).SequenceEqual(new[] { nearby.Id, old.Id, inside.Id }),
            "matches only same-category active reports inside the actual 150m circle, nearest first");
        Check(matches.Any(m => m.Title == old.Title), "old unresolved and Bangla reports remain discoverable");
        Check(!JsonSerializer.Serialize(matches).Contains("private.example.test") &&
            typeof(DuplicateReportMatch).GetProperty("Description") == null && typeof(DuplicateReportMatch).GetProperty("ImagePath") == null,
            "suggestions omit reporter identity, private descriptions, addresses and photos");
        Check((await detection.FindAsync(road.Id, double.NaN, 90.4, "citizen")).Count == 0 &&
            (await detection.FindAsync(road.Id, 0, 0, "citizen")).Count == 0,
            "invalid and out-of-city coordinates cannot search reports");
        settings.Value.RadiusMeters = 50;
        Check((await detection.FindAsync(road.Id, 23.8, 90.4, "citizen")).Count == 1, "search radius is configurable");
        settings.Value.RadiusMeters = 150;
        settings.Value.MaxResults = 1;
        Check((await detection.FindAsync(road.Id, 23.8, 90.4, "citizen")).Single().Id == nearby.Id, "result limit preserves nearest match");
        settings.Value.MaxResults = 5;

        var draft = Draft(road.Id);
        var token = tokens.Create("citizen", draft, road.Id, matches.Select(m => m.Id));
        Check(tokens.Covers(token, "citizen", draft, road.Id, new[] { nearby.Id }), "acknowledgement remains valid when a candidate closes");
        Check(!tokens.Covers(token, "citizen", draft, road.Id, new[] { outside.Id }), "new matches require a fresh acknowledgement");
        Check(tokens.Read(token + "tampered", "citizen") == null && tokens.Read(token, "other") == null,
            "suggestion tickets reject tampering and cross-account use");
        draft.Longitude = 90.41;
        Check(!tokens.Covers(token, "citizen", draft, road.Id, new[] { nearby.Id }), "acknowledgements are bound to the reviewed location");
        draft = Draft(road.Id);
        Check(!tokens.Covers(token, "citizen", draft, waste.Id, new[] { nearby.Id }), "acknowledgements are bound to category");
        clock.Now = clock.Now.AddMinutes(31);
        Check(tokens.Read(token, "citizen") == null, "suggestion tickets expire after 30 minutes");
        clock.Now = DateTimeOffset.UtcNow;

        var reviewController = ControllerFor("citizen");
        var reviewed = (ReportReviewViewModel)((ViewResult)await reviewController.Review(Draft(road.Id), null)).Model!;
        Check(reviewed.DuplicatesChecked && reviewed.DuplicateMatches.Count == 3, "AI-category review shows nearby reports before submission");
        ai.Category = null;
        var manualController = ControllerFor("citizen", manual: true);
        var manual = (ReportReviewViewModel)((ViewResult)await manualController.Review(Draft(road.Id), null)).Model!;
        Check(manual.RequiresManualCategory && !manual.DuplicatesChecked, "AI outage leaves manual category selection available");
        manual.SelectedCategoryId = road.Id;
        manualController.Request.Headers["X-Requested-With"] = "XMLHttpRequest";
        var manualResult = (PartialViewResult)await manualController.CheckDuplicates(manual);
        Check(((ReportReviewViewModel)manualResult.Model!).DuplicateMatches.Count == 3, "manual category changes return matching reports as a partial view");
        manualController.Request.Headers.Remove("X-Requested-With");
        Check(await manualController.CheckDuplicates(manual) is ViewResult, "duplicate checking also works without JavaScript");
        ai.Category = road.Name;

        var blocked = ControllerFor("citizen");
        var attempted = Draft(road.Id);
        var countBefore = await db.Reports.CountAsync();
        var warning = (ViewResult)await blocked.Confirm(attempted);
        Check(warning.ViewName == "Review" && await db.Reports.CountAsync() == countBefore,
            "confirmation with nearby reports returns a warning instead of inserting a duplicate");
        var forged = Draft(road.Id); forged.SubmitAsSeparateReport = true;
        Check(await ControllerFor("citizen").Confirm(forged) is ViewResult, "checking the override alone cannot skip review");
        var accepted = Draft(road.Id); accepted.SubmitAsSeparateReport = true;
        accepted.DuplicateReviewToken = tokens.Create("citizen", accepted, road.Id, matches.Select(m => m.Id));
        Check(await ControllerFor("citizen").Confirm(accepted) is RedirectToActionResult && await db.Reports.CountAsync() == countBefore + 1,
            "a reviewed, explicitly different issue can be submitted");
        var stale = Draft(road.Id); stale.SubmitAsSeparateReport = true; stale.DuplicateReviewToken = token;
        Check(await ControllerFor("citizen").Confirm(stale) is ViewResult && !stale.SubmitAsSeparateReport,
            "a report added after review is detected at final confirmation");
        var noMatchDraft = Draft(road.Id); noMatchDraft.Latitude = 23.85;
        Check(await ControllerFor("citizen").Confirm(noMatchDraft) is RedirectToActionResult,
            "a different location with no matches submits normally");
        var invalidPin = Draft(road.Id); invalidPin.Latitude = double.PositiveInfinity;
        Check(await ControllerFor("citizen").Confirm(invalidPin) is ViewResult, "final submission independently rejects invalid coordinates");

        token = tokens.Create("citizen", Draft(road.Id), road.Id, new[] { nearby.Id });
        Check(await ControllerFor("citizen").Support(outside.Id, token) is BadRequestObjectResult,
            "support rejects a report ID not included in its signed suggestions");
        Check(await ControllerFor("other").Support(nearby.Id, token) is BadRequestObjectResult,
            "a different citizen cannot replay a support ticket");
        var supported = (RedirectToActionResult)await ControllerFor("citizen").Support(nearby.Id, token);
        await ControllerFor("citizen").Support(nearby.Id, token);
        Check(supported.ActionName == "SupportedReport" && await db.ReportSupports.CountAsync(s => s.ReportId == nearby.Id) == 1,
            "supporting twice creates one support and opens the progress page");
        Check(await support.AddAsync(nearby.Id, "owner") == SupportResult.OwnReport, "owners cannot inflate their own support count");
        Check(await support.AddAsync(resolved.Id, "citizen") == SupportResult.Closed &&
            await support.AddAsync(rejected.Id, "citizen") == SupportResult.Closed, "closed issues reject new supporters");
        await using (var duplicateDb = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options))
        {
            duplicateDb.ReportSupports.Add(new ReportSupport { ReportId = nearby.Id, UserId = "citizen" });
            try { await duplicateDb.SaveChangesAsync(); throw new Exception("Duplicate support inserted"); }
            catch (DbUpdateException ex) when (ex.InnerException is SqliteException { SqliteErrorCode: 19 })
            { Console.WriteLine("PASS: database uniqueness prevents concurrent duplicate supports"); }
        }
        Check((await detection.FindAsync(road.Id, 23.8, 90.4, "citizen")).Single(m => m.Id == nearby.Id).AlreadySupported,
            "suggestions identify existing support");
        Check((await detection.FindAsync(road.Id, 23.8, 90.4, "owner")).Single(m => m.Id == nearby.Id).IsOwnReport,
            "suggestions distinguish the citizen's own reports");

        Check(await ControllerFor("other").Details(nearby.Id) is ForbidResult, "unrelated citizens cannot access report details");
        Check(await ControllerFor("other").SupportedReport(nearby.Id) is NotFoundResult, "progress pages require an existing support");
        Check(((RedirectToActionResult)await ControllerFor("citizen").Details(nearby.Id)).ActionName == "SupportedReport",
            "notification links route supporters to their limited progress page");
        Check(await ControllerFor("citizen").AddComment(nearby.Id, "Unauthorized private comment") is ForbidResult,
            "support does not grant access to the owner's private conversation");
        Check(((RedirectToActionResult)await ControllerFor("citizen").Track(nearby.TrackingNumber)).ActionName == "SupportedReport",
            "supporters can track supported reports");
        var summary = (SupportedReportViewModel)((ViewResult)await ControllerFor("citizen").SupportedReport(nearby.Id)).Model!;
        Check(summary.Id == nearby.Id && !JsonSerializer.Serialize(summary).Contains("Private evidence"),
            "supported progress exposes only the shared report summary");
        var myReports = (MyReportsViewModel)((ViewResult)await ControllerFor("citizen").MyReports()).Model!;
        Check(myReports.SupportedReports.Single().Id == nearby.Id && myReports.SubmittedReports.All(r => r.UserId == "citizen"),
            "My Reports separates supported issues from owned submissions");

        var admin = new AdminController(db, userManager);
        SetContext(admin, "admin", isAdmin: true);
        await admin.UpdateStatus(nearby.Id, ReportStatus.InProgress, Priority.Medium, "Private internal detail");
        Check(await db.Notifications.CountAsync(n => n.ReportId == nearby.Id && n.UserId == "citizen") == 1 &&
            await db.Notifications.AnyAsync(n => n.ReportId == nearby.Id && n.UserId == "owner"),
            "status updates notify both the owner and each supporter");
        await admin.UpdateStatus(nearby.Id, ReportStatus.InProgress, Priority.High, "Priority only");
        Check(await db.Notifications.CountAsync(n => n.ReportId == nearby.Id && n.UserId == "citizen") == 1,
            "priority-only updates do not repeat supporter status notifications");
        var progress = (SupportedReportViewModel)((ViewResult)await ControllerFor("citizen").SupportedReport(nearby.Id)).Model!;
        Check(progress.History.Count > 0 && !JsonSerializer.Serialize(progress).Contains("Private internal detail"),
            "supporters see status history without private staff remarks or identities");
        await support.AddAsync(nearby.Id, "other");
        await admin.Report(nearby.Id);
        var roster = (IReadOnlyList<AdminReportSupporterViewModel>)admin.ViewData["Supporters"]!;
        Check((int)admin.ViewData["SupportCount"]! == 2 && roster.Count == 2 &&
            roster.Select(s => s.FullName).ToHashSet().SetEquals(new[] { "Private citizen", "Private other" }) &&
            roster[0].SupportedAt >= roster[1].SupportedAt,
            "admin sees the exact supporter count, names and support dates, newest first");
        await admin.Index(null, null, null);
        Check(((IReadOnlyDictionary<int, int>)admin.ViewData["SupportCounts"]!)[nearby.Id] == 2,
            "admin report listing includes supporter counts");
        await ControllerFor("citizen").StopSupporting(nearby.Id);
        await admin.Report(nearby.Id);
        Check(((IReadOnlyList<AdminReportSupporterViewModel>)admin.ViewData["Supporters"]!).Single().FullName == "Private other",
            "admin supporter roster removes citizens who stop supporting");
        Check(!await db.ReportSupports.AnyAsync(s => s.ReportId == nearby.Id && s.UserId == "citizen") &&
            await db.ReportSupports.AnyAsync(s => s.ReportId == nearby.Id && s.UserId == "other"),
            "stopping support removes only the current citizen's support");
        await admin.UpdateStatus(nearby.Id, ReportStatus.Resolved, Priority.Medium, "Done");
        Check(await db.Notifications.CountAsync(n => n.ReportId == nearby.Id && n.UserId == "citizen") == 1 &&
            await db.Notifications.AnyAsync(n => n.ReportId == nearby.Id && n.UserId == "other"),
            "unsubscribed citizens stop receiving status updates while other supporters still receive them");
        Check(await ControllerFor("citizen").SupportedReport(nearby.Id) is NotFoundResult,
            "stopping support also removes access to its progress page");
        await CheckMvcFlowAsync(connection, ai, old.Id, road.Id);
        Console.WriteLine("All duplicate report checks passed using an isolated in-memory database. No AI or external requests were made.");

        ReportController ControllerFor(string userId, bool manual = false)
        {
            var controller = new ReportController(db, userManager, new FakeEnvironment(), ai,
                NullLogger<ReportController>.Instance, detection, tokens, support);
            SetContext(controller, userId);
            controller.TempData["ReportReviewPriority"] = "Low";
            if (!manual) controller.TempData["ReportReviewAICategoryId"] = road.Id.ToString();
            return controller;
        }
    }

    private static async Task CheckMvcFlowAsync(SqliteConnection connection, TestAI ai, int existingReportId, int categoryId)
    {
        // Render the real Razor views and exercise routing, model binding, TempData and antiforgery.
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ApplicationName = typeof(ReportController).Assembly.GetName().Name,
            EnvironmentName = "Development"
        });
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddControllersWithViews().AddApplicationPart(typeof(ReportController).Assembly);
        builder.Services.AddDbContext<ApplicationDbContext>(o => o.UseSqlite(connection));
        builder.Services.AddIdentity<ApplicationUser, IdentityRole>().AddEntityFrameworkStores<ApplicationDbContext>();
        builder.Services.AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider());
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.Configure<DuplicateReportOptions>(_ => { });
        builder.Services.AddScoped<DuplicateReportService>();
        builder.Services.AddScoped<DuplicateReviewTokens>();
        builder.Services.AddScoped<ReportSupportService>();
        builder.Services.AddSingleton<IAIService>(ai);
        await using var app = builder.Build();
        app.UseRouting();
        app.UseAuthentication();
        // Only this isolated test host supplies a test principal; production authentication is unchanged.
        app.Use(async (context, next) =>
        {
            var isAdmin = context.Request.Headers["X-Test-Role"] == "Admin";
            var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, isAdmin ? "admin" : "citizen") };
            if (isAdmin) claims.Add(new Claim(ClaimTypes.Role, "Admin"));
            context.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
            await next();
        });
        app.UseAuthorization();
        app.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}");
        await app.StartAsync();
        try
        {
            using var handler = new HttpClientHandler { AllowAutoRedirect = false, CookieContainer = new CookieContainer() };
            using var client = new HttpClient(handler) { BaseAddress = new Uri(app.Urls.Single()) };
            var create = await client.GetStringAsync("/Report/Create");
            var fields = new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = HiddenInputs(create)["__RequestVerificationToken"],
                ["Title"] = "Road issue integration check", ["Description"] = "Private HTTP evidence",
                ["Address"] = "Private HTTP address", ["Latitude"] = "23.8", ["Longitude"] = "90.4"
            };
            ai.Category = null;
            var review = await client.PostAsync("/Report/Review", new FormUrlEncodedContent(fields));
            var reviewHtml = await review.Content.ReadAsStringAsync();
            Check(review.StatusCode == HttpStatusCode.OK && reviewHtml.Contains("report-review-form"),
                "HTTP review renders the actual report form with manual category fallback");
            fields = HiddenInputs(reviewHtml);
            fields["SelectedCategoryId"] = categoryId.ToString();
            using var request = new HttpRequestMessage(HttpMethod.Post, "/Report/CheckDuplicates")
            { Content = new FormUrlEncodedContent(fields) };
            request.Headers.Add("X-Requested-With", "XMLHttpRequest");
            var partial = await client.SendAsync(request);
            var partialHtml = await partial.Content.ReadAsStringAsync();
            Check(partial.StatusCode == HttpStatusCode.OK && partialHtml.Contains("formaction=\"/Report/Support\"") &&
                partialHtml.Contains("name=\"DuplicateReviewToken\""),
                "AJAX response renders support actions and a protected review ticket");
            fields["DuplicateReviewToken"] = HiddenInputs(partialHtml)["DuplicateReviewToken"];
            fields["reportId"] = existingReportId.ToString();
            var support = await client.PostAsync("/Report/Support", new FormUrlEncodedContent(fields));
            Check(support.StatusCode == HttpStatusCode.Redirect && support.Headers.Location!.ToString().Contains("SupportedReport"),
                "real form submission binds the support action and redirects to progress");
            var progress = await client.GetStringAsync(support.Headers.Location);
            Check(progress.Contains("Your support has been added") && !progress.Contains("Private evidence description"),
                "supported progress renders successfully without private report content");
            Check(!progress.Contains("report-supporters-heading"), "citizen progress pages do not expose the admin supporter roster");
            var deniedAdmin = await client.GetAsync($"/Admin/Report/{existingReportId}");
            Check(deniedAdmin.StatusCode is HttpStatusCode.Redirect or HttpStatusCode.Forbidden &&
                !(await deniedAdmin.Content.ReadAsStringAsync()).Contains("Issue Supporters"),
                "non-admin citizens cannot access supporter names through the admin route");
            using var adminClient = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { BaseAddress = client.BaseAddress };
            adminClient.DefaultRequestHeaders.Add("X-Test-Role", "Admin");
            var rosterHtml = await adminClient.GetStringAsync($"/Admin/Report/{existingReportId}");
            Check(rosterHtml.Contains("Issue Supporters") && rosterHtml.Contains("Private citizen") && rosterHtml.Contains("1 supporter"),
                "admin management page renders supporter names and their count");
            Check((await adminClient.GetStringAsync("/Admin")).Contains($"/Admin/Report/{existingReportId}#report-supporters"),
                "admin listing links directly to each report's supporter roster");
            var withoutToken = await client.PostAsync("/Report/StopSupporting", new FormUrlEncodedContent(new Dictionary<string, string>
            { ["reportId"] = existingReportId.ToString() }));
            Check(withoutToken.StatusCode == HttpStatusCode.BadRequest, "MVC antiforgery rejects support changes without a token");
            var stopFields = HiddenInputs(progress);
            stopFields["reportId"] = existingReportId.ToString();
            var stop = await client.PostAsync("/Report/StopSupporting", new FormUrlEncodedContent(stopFields));
            Check(stop.StatusCode == HttpStatusCode.Redirect && (await client.GetStringAsync("/Report/MyReports")).Contains("Reports I Support"),
                "stop-support and My Reports Razor pages work through MVC");
            Check((await adminClient.GetStringAsync($"/Admin/Report/{existingReportId}")).Contains("No citizens have supported this report yet."),
                "admin roster shows an empty state after the final supporter leaves");
        }
        finally { await app.StopAsync(); }
    }

    private static Dictionary<string, string> HiddenInputs(string html)
    {
        var values = new Dictionary<string, string>();
        foreach (Match input in Regex.Matches(html, "<input\\b[^>]*>", RegexOptions.IgnoreCase))
        {
            var attributes = Regex.Matches(input.Value, "([\\w-]+)=\"([^\"]*)\"")
                .Cast<Match>().ToDictionary(m => m.Groups[1].Value, m => WebUtility.HtmlDecode(m.Groups[2].Value), StringComparer.OrdinalIgnoreCase);
            if (attributes.GetValueOrDefault("type") == "hidden" && attributes.TryGetValue("name", out var name))
                values[name] = attributes.GetValueOrDefault("value", "");
        }
        return values;
    }

    private static ReportReviewViewModel Draft(int categoryId) => new()
    {
        Title = "Broken road near the park", Description = "Private evidence description", Address = "Private exact address",
        Latitude = 23.8, Longitude = 90.4, SelectedCategoryId = categoryId
    };
    private static Report ReportAt(string title, int category, double metresNorth, ReportStatus status = ReportStatus.Pending) => new()
    {
        Title = title, Description = "Private evidence description", Address = "Private exact address", ImagePath = "/private-photo.jpg",
        UserId = "owner", CategoryId = category, CurrentStatus = status, Latitude = 23.8 + metresNorth / 111_195,
        Longitude = 90.4, TrackingNumber = "GCR-TEST-" + Guid.NewGuid().ToString("N")
    };
    private static void SetContext(Controller controller, string userId, bool isAdmin = false)
    {
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, userId) };
        if (isAdmin) claims.Add(new Claim(ClaimTypes.Role, "Admin"));
        controller.ControllerContext = new ControllerContext
        { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test")) } };
        controller.TempData = new TempDataDictionary(controller.HttpContext, new MemoryTempData());
    }
    private static void Check(bool condition, string name)
    { if (!condition) throw new Exception("FAIL: " + name); Console.WriteLine("PASS: " + name); }
    private sealed class TestClock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => Now;
    }
    private sealed class TestAI : IAIService
    {
        public string? Category { get; set; }
        public Task<string?> CategorizeReportAsync(string title, string description, IEnumerable<string> availableCategories, CancellationToken cancellationToken = default) => Task.FromResult(Category);
        public Task<Priority?> DetectPriorityAsync(string title, string description, CancellationToken cancellationToken = default) => Task.FromResult<Priority?>(Priority.Low);
        public Task<string?> SummarizeReportAsync(string title, string description, CancellationToken cancellationToken = default) => Task.FromResult<string?>(null);
        public Task<string?> ChatAsync(string message, string? context = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public IAsyncEnumerable<string> ChatStreamAsync(string message, string? context = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
