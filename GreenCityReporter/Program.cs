using GreenCityReporter.Data;
using GreenCityReporter.Models;
using GreenCityReporter.Services.AI;
using GreenCityReporter.Services.Assignment;
using GreenCityReporter.Services.Background;
using GreenCityReporter.Services.Chat;
using GreenCityReporter.Services.Email;
using GreenCityReporter.Services.Payments;
using GreenCityReporter.Services.Reports;
using GreenCityReporter.Services.Storage;
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
    .Validate(o => double.IsFinite(o.RadiusMeters) && o.RadiusMeters is >= 25 and <= 1000 &&
        o.MaxResults is >= 1 and <= 20, "Configure a duplicate radius of 25–1000 meters and 1–20 results.")
    .ValidateOnStart();
builder.Services.AddScoped<DuplicateReportService>();
builder.Services.AddScoped<DuplicateReviewTokens>();
builder.Services.AddScoped<ReportSupportService>();
builder.Services.AddHealthChecks();
builder.Services.AddOptions<PaymentOptions>()
    .Configure(options => PaymentOptionsSetup.Configure(options, builder.Configuration))
    .Validate(o => o.DemoMode || !o.Enabled || o.IsReady, "Configure valid SSLCommerz credentials, organization contact details, and a public HTTPS application URL.")
    .ValidateOnStart();
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
    options.AddPolicy("account-email", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 5, Window = TimeSpan.FromMinutes(15), QueueLimit = 0 }));
});

builder.Services.Configure<AIOptions>(builder.Configuration.GetSection("AI"));
builder.Services.Configure<ReportMonitoringOptions>(builder.Configuration.GetSection("ReportMonitoring"));
builder.Services.Configure<StorageOptions>(builder.Configuration.GetSection("Storage"));
builder.Services.AddOptions<EmailOptions>()
    .BindConfiguration("Email")
    .Validate(options => !builder.Environment.IsProduction() || (options.IsReady && options.EnableSsl),
        "Production email configuration is required. Set Email__SmtpHost, Email__SmtpPort, Email__SenderName, Email__SenderEmail, Email__Username, Email__Password, and Email__EnableSsl.")
    .Validate(options => !builder.Environment.IsProduction() ||
        (Uri.TryCreate(options.PublicBaseUrl, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps),
        "Production requires Email__PublicBaseUrl to be an absolute HTTPS URL.")
    .ValidateOnStart();
builder.Services.AddScoped<IAccountEmailSender, SmtpAccountEmailSender>();

var storageProvider = builder.Configuration["Storage:Provider"]?.Trim();
if (string.IsNullOrWhiteSpace(storageProvider))
{
    storageProvider = "Local";
}

if (!string.Equals(storageProvider, "Local", StringComparison.OrdinalIgnoreCase) &&
    !string.Equals(storageProvider, "Supabase", StringComparison.OrdinalIgnoreCase))
{
    if (builder.Environment.IsProduction())
    {
        throw new InvalidOperationException($"Unknown storage provider '{storageProvider}'. Configure Storage:Provider as Local or Supabase.");
    }

    builder.Logging.AddFilter("GreenCityReporter.Services.Storage", LogLevel.Warning);
    storageProvider = "Local";
}

if (string.Equals(storageProvider, "Supabase", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddHttpClient<SupabaseStorageService>(client => client.Timeout = TimeSpan.FromSeconds(30));
    builder.Services.AddScoped<IFileStorageService, SupabaseStorageService>();
}
else
{
    builder.Services.AddScoped<IFileStorageService, LocalFileStorageService>();
}

builder.Services.AddHostedService<ReportMonitoringService>();
builder.Services.AddScoped<IChatService, GreenCityChatService>();
builder.Services.AddScoped<IReportAssignmentService, ReportAssignmentService>();

builder.Services.AddHttpClient("Ollama", (sp, client) =>
{
    var opts = sp.GetRequiredService<IOptions<AIOptions>>().Value;
    client.Timeout = TimeSpan.FromSeconds(Math.Max(5, opts.TimeoutSeconds));
    try
    {
        client.BaseAddress = new Uri(opts.Ollama.BaseUrl);
    }
    catch
    {
        // Leave BaseAddress unset and rely on the service to log warnings.
    }
});

builder.Services.AddHttpClient("Groq", (sp, client) =>
{
    var opts = sp.GetRequiredService<IOptions<AIOptions>>().Value;
    client.Timeout = TimeSpan.FromSeconds(Math.Max(5, opts.TimeoutSeconds));
    try
    {
        client.BaseAddress = new Uri(opts.Groq.BaseUrl);
    }
    catch
    {
        // Leave BaseAddress unset and rely on the service to log warnings.
    }
});

builder.Services.AddScoped<IAIService>(sp =>
{
    var opts = sp.GetRequiredService<IOptions<AIOptions>>().Value;
    var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
    var httpFactory = sp.GetRequiredService<IHttpClientFactory>();
    var providerName = (opts.Provider ?? "Ollama").Trim();
    var normalizedProvider = providerName.ToLowerInvariant();

    if (normalizedProvider == "ollama")
    {
        return new OllamaAIService(httpFactory.CreateClient("Ollama"), loggerFactory.CreateLogger<OllamaAIService>(), Options.Create(opts));
    }

    if (normalizedProvider == "groq")
    {
        return new GroqAIService(httpFactory.CreateClient("Groq"), loggerFactory.CreateLogger<GroqAIService>(), Options.Create(opts));
    }

    loggerFactory.CreateLogger<Program>().LogWarning("Unknown AI provider '{Provider}'. Falling back to Ollama.", providerName);
    return new OllamaAIService(httpFactory.CreateClient("Ollama"), loggerFactory.CreateLogger<OllamaAIService>(), Options.Create(opts));
});

var databaseProvider = builder.Configuration["Database:Provider"]?.Trim();
var databaseConnectionString = builder.Configuration.GetConnectionString("DefaultConnection");

if (string.Equals(databaseProvider, "PostgreSQL", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddDbContext<PostgreSqlApplicationDbContext>(options =>
        options.UseNpgsql(
            databaseConnectionString,
            npgsqlOptions => npgsqlOptions.EnableRetryOnFailure()));
    builder.Services.AddScoped<ApplicationDbContext>(services =>
        services.GetRequiredService<PostgreSqlApplicationDbContext>());
}
else
{
    builder.Services.AddDbContext<ApplicationDbContext>(options =>
        options.UseSqlServer(
            databaseConnectionString,
            sqlOptions => sqlOptions.EnableRetryOnFailure()));
}

builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options => {
    options.Password.RequireDigit = true;
    options.Password.RequiredLength = 6;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = true;
    options.Password.RequireLowercase = true;
    options.SignIn.RequireConfirmedEmail = true;
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

var startupLogger = app.Services.GetRequiredService<ILogger<Program>>();
var aiOptions = app.Services.GetRequiredService<IOptions<AIOptions>>().Value;
startupLogger.LogInformation("AI Provider: {Provider}", aiOptions.Provider ?? "Ollama");
startupLogger.LogInformation("Groq API key configured: {HasApiKey}", !string.IsNullOrWhiteSpace(aiOptions.Groq.ApiKey));
startupLogger.LogInformation("Groq model: {Model}", string.IsNullOrWhiteSpace(aiOptions.Groq.Model) ? "openai/gpt-oss-20b" : aiOptions.Groq.Model);
startupLogger.LogInformation("Groq endpoint: {Endpoint}", string.IsNullOrWhiteSpace(aiOptions.Groq.BaseUrl)
    ? "https://api.groq.com/openai/v1/chat/completions"
    : new Uri(new Uri(aiOptions.Groq.BaseUrl.TrimEnd('/') + "/"), "chat/completions").ToString());

// Seed data
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var database = services.GetRequiredService<ApplicationDbContext>();
        await database.Database.MigrateAsync();
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

app.MapHealthChecks("/health");
app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();


app.Run();
