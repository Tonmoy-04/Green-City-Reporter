using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GreenCityReporter.ViewModels;
using Microsoft.AspNetCore.DataProtection;

namespace GreenCityReporter.Services.Reports;

public sealed record DuplicateReviewTicket(string UserId, string Fingerprint, int CategoryId,
    double Latitude, double Longitude, int[] ReportIds, DateTimeOffset ExpiresAt);

public sealed class DuplicateReviewTokens(IDataProtectionProvider provider, TimeProvider clock)
{
    private readonly IDataProtector protector = provider.CreateProtector("GreenCityReporter.DuplicateReview.v1");

    public string Create(string userId, ReportReviewViewModel model, int categoryId, IEnumerable<int> reportIds) =>
        protector.Protect(JsonSerializer.Serialize(new DuplicateReviewTicket(userId, Fingerprint(model, categoryId),
            categoryId, model.Latitude!.Value, model.Longitude!.Value, reportIds.ToArray(), clock.GetUtcNow().AddMinutes(30))));

    public DuplicateReviewTicket? Read(string? token, string userId)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length > 16000) return null;
        try
        {
            var ticket = JsonSerializer.Deserialize<DuplicateReviewTicket>(protector.Unprotect(token));
            return ticket?.UserId == userId && ticket.ExpiresAt > clock.GetUtcNow() ? ticket : null;
        }
        catch (Exception ex) when (ex is CryptographicException or JsonException or FormatException) { return null; }
    }

    public bool Covers(string? token, string userId, ReportReviewViewModel model, int categoryId,
        IEnumerable<int> currentReportIds)
    {
        var ticket = Read(token, userId);
        return ticket != null && ticket.Fingerprint == Fingerprint(model, categoryId) &&
            currentReportIds.All(id => ticket.ReportIds.Contains(id));
    }

    private static string Fingerprint(ReportReviewViewModel model, int categoryId) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new
        {
            model.Title, model.Description, model.Address, model.Latitude, model.Longitude, model.ImagePath, CategoryId = categoryId
        }))));
}
