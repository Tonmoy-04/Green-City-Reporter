using GreenCityReporter.Data;
using GreenCityReporter.Models;
using GreenCityReporter.Services.AI;
using GreenCityReporter.Services.Background;
using GreenCityReporter.Services.Chat;
using GreenCityReporter.Services.Payments;
using GreenCityReporter.Services.Reports;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddDataProtection();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddOptions<DuplicateReportOptions>().BindConfiguration("DuplicateReports")
    .Validate(o => double.IsFinite(o.RadiusMeters) && o.RadiusMeters is >= 25 and <= 1000 && o.MaxResults is >= 1 and <= 20,
        "DuplicateReports requires RadiusMeters between 25 and 1000 and MaxResults between 1 and 20.").ValidateOnStart();
builder.Services.AddScoped<DuplicateReportService>();
builder.Services.AddScoped<DuplicateReviewTokens>();
builder.Services.AddScoped<ReportSupportService>();
builder.Services.AddOptions<PaymentOptions>().BindConfiguration("Donations:Gateway").Validate(o => o.DemoMode || !o.Enabled || o.IsReady, "Configure valid payment credentials, organization contact details, and a public HTTPS base URL.").ValidateOnStart();
builder.Services.AddOptions<DonationEmailOptions>().BindConfiguration("Donations:Email").Validate(o => !o.Enabled || (o.IsReady && (builder.Environment.IsDevelopment() || o.UseStartTls)), "Configure a valid SMTP server and sender. Production email requires STARTTLS.").ValidateOnStart();
builder.Services.AddHttpClient<IDonationGateway, SslCommerzGateway>(client => client.Timeout = TimeSpan.FromSeconds(25))
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false })
    .RemoveAllLoggers(); // The validation API uses credentials in its query string; never log gateway URLs.
builder.Services.AddScoped<DonationPaymentService>();
builder.Services.AddScoped<IDonationReceiptSender, SmtpDonationReceiptSender>();
builder.Services.AddScoped<DonationReceiptEmailService>();
builder.Services.AddHostedService<DonationProcessingWorker>();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("donation-checkout", context => RateLimitPartition.GetFixedWindowLimiter(
        context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
});

// Bind AI options
builder.Services.Configure<AIOptions>(
    builder.Configuration.GetSection("AI"));

builder.Services.Configure<ReportMonitoringOptions>(
    builder.Configuration.GetSection("ReportMonitoring"));

builder.Services.AddHostedService<ReportMonitoringService>();
builder.Services.AddScoped<IChatService, GreenCityChatService>();

// Register Ollama AI service with typed HttpClient
builder.Services.AddHttpClient<IAIService, OllamaAIService>((sp, client) =>
{
    var opts = sp.GetRequiredService<IOptions<AIOptions>>().Value;
    try
    {
        client.BaseAddress = new Uri(opts.Ollama.BaseUrl);
    }
    catch
    {
        // If invalid, leave BaseAddress unset; OllamaAIService will log a warning
    }

    client.Timeout = TimeSpan.FromSeconds(opts.TimeoutSeconds);
});

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        sqlOptions => sqlOptions.EnableRetryOnFailure()));

builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options => {
    options.Password.RequireDigit = true;
    options.Password.RequiredLength = 6;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = true;
    options.Password.RequireLowercase = true;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Account/AccessDenied";
});

var app = builder.Build();

// Seed data
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        await DatabaseSeeder.SeedAsync(services);
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred seeding the DB.");
    }
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();


app.Run();
