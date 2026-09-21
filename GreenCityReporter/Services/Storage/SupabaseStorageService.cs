using System.Net;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GreenCityReporter.Services.Storage
{
    public sealed class SupabaseStorageService : IFileStorageService
    {
        private const string LocalPathPrefix = "/uploads/reports/";
        private readonly HttpClient _httpClient;
        private readonly SupabaseStorageOptions _options;
        private readonly ILogger<SupabaseStorageService> _logger;

        public SupabaseStorageService(
            HttpClient httpClient,
            IOptions<StorageOptions> options,
            ILogger<SupabaseStorageService> logger)
        {
            _httpClient = httpClient;
            _options = options.Value.Supabase;
            _logger = logger;
        }

        public async Task<string?> SaveReportImageAsync(IFormFile file, CancellationToken cancellationToken = default)
        {
            if (!ReportImageValidation.IsValid(file) ||
                string.IsNullOrWhiteSpace(_options.Url) ||
                string.IsNullOrWhiteSpace(_options.ServiceRoleKey) ||
                string.IsNullOrWhiteSpace(_options.Bucket))
            {
                _logger.LogWarning("Supabase report image upload rejected because the image or storage configuration is invalid.");
                return null;
            }

            var objectPath = $"reports/{DateTime.UtcNow:yyyy/MM}/{Guid.NewGuid():N}{ReportImageValidation.GetSafeExtension(file)}";
            var endpoint = BuildObjectUri(objectPath);

            using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ServiceRoleKey);
            request.Headers.Add("apikey", _options.ServiceRoleKey);
            request.Headers.Add("x-upsert", "false");
            request.Content = new StreamContent(file.OpenReadStream());
            request.Content.Headers.ContentType = new MediaTypeHeaderValue(file.ContentType);

            try
            {
                using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    var responseBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                    _logger.LogWarning(
                        "Supabase report image upload failed. Status={StatusCode}, Error={ResponseBody}, Bucket={Bucket}, ObjectPath={ObjectPath}",
                        response.StatusCode,
                        SanitizeResponseBody(responseBody),
                        _options.Bucket,
                        objectPath);
                    return null;
                }

                return BuildPublicUrl(objectPath);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Supabase report image upload failed.");
                return null;
            }
        }

        public async Task DeleteReportImageAsync(string? storedPath, CancellationToken cancellationToken = default)
        {
            if (!TryGetObjectPath(storedPath, out var objectPath) ||
                string.IsNullOrWhiteSpace(_options.ServiceRoleKey) ||
                string.IsNullOrWhiteSpace(_options.Bucket))
            {
                return;
            }

            using var request = new HttpRequestMessage(HttpMethod.Delete, BuildObjectUri(objectPath));
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ServiceRoleKey);
            request.Headers.Add("apikey", _options.ServiceRoleKey);

            try
            {
                using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode && response.StatusCode != HttpStatusCode.NotFound)
                {
                    var responseBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                    _logger.LogWarning(
                        "Supabase report image deletion failed. Status={StatusCode}, Error={ResponseBody}, Bucket={Bucket}, ObjectPath={ObjectPath}",
                        response.StatusCode,
                        SanitizeResponseBody(responseBody),
                        _options.Bucket,
                        objectPath);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Supabase report image deletion failed.");
            }
        }

        public bool IsValidReportImagePath(string? storedPath)
        {
            return TryGetObjectPath(storedPath, out _);
        }

        private Uri BuildObjectUri(string objectPath)
        {
            return BuildStoragePath($"object/{EscapePathSegment(_options.Bucket)}/{EscapeObjectPath(objectPath)}");
        }

        private string BuildPublicUrl(string objectPath)
        {
            return BuildStoragePath($"object/public/{EscapePathSegment(_options.Bucket)}/{EscapeObjectPath(objectPath)}").ToString();
        }

        private Uri BuildStoragePath(string path)
        {
            var baseUrl = NormalizeSupabaseUrl();
            return new Uri(baseUrl, $"storage/v1/{path}");
        }

        private Uri NormalizeSupabaseUrl()
        {
            if (!Uri.TryCreate(_options.Url, UriKind.Absolute, out var configuredUrl) ||
                configuredUrl.Scheme != Uri.UriSchemeHttps)
            {
                throw new InvalidOperationException("Storage:Supabase:Url must be an HTTPS Supabase project URL.");
            }

            var builder = new UriBuilder(configuredUrl)
            {
                Path = "/",
                Query = string.Empty,
                Fragment = string.Empty
            };
            return builder.Uri;
        }

        private static string EscapeObjectPath(string objectPath)
        {
            return string.Join('/', objectPath.Split('/', StringSplitOptions.RemoveEmptyEntries).Select(EscapePathSegment));
        }

        private static string EscapePathSegment(string segment)
        {
            return Uri.EscapeDataString(segment);
        }

        private static string SanitizeResponseBody(string responseBody)
        {
            if (string.IsNullOrWhiteSpace(responseBody))
            {
                return "<empty>";
            }

            return responseBody.Length <= 2048 ? responseBody : responseBody[..2048];
        }

        private bool TryGetObjectPath(string? storedPath, out string objectPath)
        {
            objectPath = string.Empty;
            if (string.IsNullOrWhiteSpace(storedPath) || !Uri.TryCreate(storedPath, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
            {
                return false;
            }

            if (!Uri.TryCreate(_options.Url, UriKind.Absolute, out var configuredUrl) ||
                configuredUrl.Scheme != Uri.UriSchemeHttps ||
                !string.Equals(uri.Host, configuredUrl.Host, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var publicPrefix = $"/storage/v1/object/public/{_options.Bucket}/";
            if (!uri.AbsolutePath.StartsWith(publicPrefix, StringComparison.Ordinal))
            {
                return false;
            }

            objectPath = uri.AbsolutePath[publicPrefix.Length..];
            var fileName = Path.GetFileName(objectPath);
            var extension = Path.GetExtension(fileName);
            return objectPath.StartsWith("reports/", StringComparison.Ordinal) &&
                   !objectPath.Contains("..", StringComparison.Ordinal) &&
                   Guid.TryParse(Path.GetFileNameWithoutExtension(fileName), out _) &&
                   new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" }.Contains(extension, StringComparer.OrdinalIgnoreCase);
        }
    }
}