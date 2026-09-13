using System.Security.Claims;

namespace GreenCityReporter.Services.Chat
{
    public interface IChatService
    {
        Task<string?> AskAsync(
            string message,
            ClaimsPrincipal user,
            CancellationToken cancellationToken = default);
    }
}
