using GreenCityReporter.Data;
using GreenCityReporter.Models;
using GreenCityReporter.Services.AI;
using GreenCityReporter.Services.Background;
using GreenCityReporter.Services.Chat;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

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

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();


app.Run();
