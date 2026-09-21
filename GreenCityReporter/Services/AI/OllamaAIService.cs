using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.CompilerServices;
using GreenCityReporter.Models.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GreenCityReporter.Services.AI
{
    public class OllamaAIService : IAIService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<OllamaAIService> _logger;
        private readonly AIOptions _options;
        private readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        };

        public OllamaAIService(
            HttpClient httpClient,
            ILogger<OllamaAIService> logger,
            IOptions<AIOptions> options)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _options = options?.Value ?? throw new ArgumentNullException(nameof(options));

            // Ensure base address if not configured externally
            if (_httpClient.BaseAddress == null)
            {
                try
                {
                    _httpClient.BaseAddress = new Uri(_options.Ollama.BaseUrl);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Invalid Ollama BaseUrl in options: {BaseUrl}", _options.Ollama.BaseUrl);
                }
            }
        }

        public async Task<ReportClassificationResult?> ClassifyReportAsync(
            string title,
            string description,
            IEnumerable<string> availableCategories,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var categories = string.Join(", ", availableCategories ?? Enumerable.Empty<string>());
                var prompt = "Classify this civic report. Choose exactly one category from: " + categories + ".\n" +
                    "Return only valid JSON with this shape: {\"category\":\"exact category name\",\"critical\":false,\"confidence\":0.0}.\n" +
                    "Set critical true only for an immediate threat such as an active fire, road accident, severe electrical hazard, gas leak, major flooding, or dangerous infrastructure failure.\n" +
                    "Confidence must be a number from 0 to 1. Do not invent categories or include markdown.\n" +
                    "Title: " + (title ?? string.Empty) + "\nDescription: " + (description ?? string.Empty);

                var raw = await SendPromptAsync(prompt, cancellationToken).ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(raw)) return null;

                var json = CleanModelOutput(raw).Trim('`', ' ', '\r', '\n');
                if (json.StartsWith("json", StringComparison.OrdinalIgnoreCase)) json = json[4..].Trim();
                using var document = JsonDocument.Parse(json);
                var root = document.RootElement;
                var category = root.TryGetProperty("category", out var categoryElement) && categoryElement.ValueKind == JsonValueKind.String
                    ? categoryElement.GetString()
                    : null;
                var critical = root.TryGetProperty("critical", out var criticalElement) && criticalElement.ValueKind == JsonValueKind.True;
                double? confidence = root.TryGetProperty("confidence", out var confidenceElement) && confidenceElement.TryGetDouble(out var value)
                    ? Math.Clamp(value, 0, 1)
                    : null;

                var matchedCategory = (availableCategories ?? Enumerable.Empty<string>())
                    .FirstOrDefault(candidate => string.Equals(candidate, category, StringComparison.OrdinalIgnoreCase));
                return matchedCategory == null || confidence is null
                    ? null
                    : new ReportClassificationResult(matchedCategory, critical, confidence);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "ClassifyReportAsync could not parse the AI response.");
                return null;
            }
        }

        public async Task<string?> CategorizeReportAsync(string title, string description, IEnumerable<string> availableCategories, CancellationToken cancellationToken = default)
        {
            try
            {
                var categories = availableCategories?.ToList() ?? new List<string>();
                var categoryDescriptions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Waste Management"] = "Issues related to waste collection, disposal, and accumulation.",
                    ["Road Damage"] = "Damaged roads, potholes, and related road problems.",
                    ["Drainage"] = "Blocked or damaged drainage systems.",
                    ["Street Lighting"] = "Broken or non-functioning street lights.",
                    ["Waterlogging"] = "Water accumulation and flooding in public areas.",
                    ["Public Infrastructure"] = "Issues involving public buildings, facilities, and infrastructure."
                };

                var promptBuilder = new StringBuilder();
                promptBuilder.AppendLine("You are classifying civic issue reports.");
                promptBuilder.AppendLine();
                promptBuilder.AppendLine("Choose exactly ONE category from the supplied list.");
                promptBuilder.AppendLine();
                promptBuilder.AppendLine("Rules:");
                promptBuilder.AppendLine("- Prefer the most specific category that directly matches the issue.");
                promptBuilder.AppendLine("- Do not choose a broad/general category if a more specific category clearly applies.");
                promptBuilder.AppendLine("- Base the decision primarily on the report title and description.");
                promptBuilder.AppendLine("- Return only the exact category name from the supplied list.");
                promptBuilder.AppendLine("- Do not explain your answer.");
                promptBuilder.AppendLine("- Do not invent categories.");
                promptBuilder.AppendLine();
                promptBuilder.AppendLine("Available categories:");

                foreach (var c in categories)
                {
                    promptBuilder.AppendLine(categoryDescriptions.TryGetValue(c, out var categoryDescription)
                        ? $"- Category: {c}\n  Description: {categoryDescription}"
                        : $"- Category: {c}");
                }

                var examples = new[]
                {
                    (Keywords: "flooded road, standing rain water, water on road, waterlogging", Category: "Waterlogging"),
                    (Keywords: "broken lamp, street light not working", Category: "Street Lighting"),
                    (Keywords: "pothole, damaged road surface", Category: "Road Damage"),
                    (Keywords: "garbage, trash, waste pile", Category: "Waste Management")
                };

                var availableExamples = examples
                    .Where(example => categories.Any(category =>
                        string.Equals(category, example.Category, StringComparison.OrdinalIgnoreCase)))
                    .ToList();

                if (availableExamples.Count > 0)
                {
                    promptBuilder.AppendLine();
                    promptBuilder.AppendLine("Examples:");

                    foreach (var example in availableExamples)
                    {
                        promptBuilder.AppendLine($"- {example.Keywords} -> {example.Category}");
                    }
                }

                promptBuilder.AppendLine();
                promptBuilder.AppendLine("Title:");
                promptBuilder.AppendLine(title ?? string.Empty);
                promptBuilder.AppendLine();
                promptBuilder.AppendLine("Description:");
                promptBuilder.AppendLine(description ?? string.Empty);

                var prompt = promptBuilder.ToString();

                var raw = await SendPromptAsync(prompt, cancellationToken).ConfigureAwait(false);

                if (string.IsNullOrWhiteSpace(raw))
                {
                    return null;
                }

                var cleaned = CleanModelOutput(raw);

                // Compare canonical categories case-insensitively
                var match = categories.FirstOrDefault(c => string.Equals(c, cleaned, StringComparison.OrdinalIgnoreCase));
                _logger.LogInformation("AI category result: {Category}", match ?? cleaned);

                return match;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                _logger.LogInformation("CategorizeReportAsync was cancelled by caller.");
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "CategorizeReportAsync failed.");
                return null;
            }
        }

        public async Task<Priority?> DetectPriorityAsync(string title, string description, CancellationToken cancellationToken = default)
        {
            try
            {
                var prompt = new StringBuilder();
                prompt.AppendLine("You are given a civic issue report. Return exactly one of the following priority labels: Low, Medium, High, Critical.");
                prompt.AppendLine();
                prompt.AppendLine("Use these definitions:");
                prompt.AppendLine("Low: Minor inconvenience with little immediate impact.");
                prompt.AppendLine("Medium: Meaningful civic issue requiring attention but without immediate serious danger.");
                prompt.AppendLine("High: Significant public safety, health, environmental, or infrastructure concern.");
                prompt.AppendLine("Critical: Immediate or severe threat to public safety, life, health, or essential infrastructure.");
                prompt.AppendLine();
                prompt.AppendLine("Title:");
                prompt.AppendLine(title ?? string.Empty);
                prompt.AppendLine();
                prompt.AppendLine("Description:");
                prompt.AppendLine(description ?? string.Empty);

                var raw = await SendPromptAsync(prompt.ToString(), cancellationToken).ConfigureAwait(false);

                if (string.IsNullOrWhiteSpace(raw))
                {
                    return null;
                }

                var cleaned = CleanModelOutput(raw);

                if (Enum.TryParse<Priority>(cleaned, true, out var priority))
                {
                    return priority;
                }

                return null;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                _logger.LogInformation("DetectPriorityAsync was cancelled by caller.");
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "DetectPriorityAsync failed.");
                return null;
            }
        }

        public async Task<string?> SummarizeReportAsync(string title, string description, CancellationToken cancellationToken = default)
        {
            try
            {
                var prompt = new StringBuilder();
                prompt.AppendLine("Create a concise 1-2 sentence administrative summary of the report below. Do not invent facts or add recommendations. Return only the summary.");
                prompt.AppendLine();
                prompt.AppendLine("Title:");
                prompt.AppendLine(title ?? string.Empty);
                prompt.AppendLine();
                prompt.AppendLine("Description:");
                prompt.AppendLine(description ?? string.Empty);

                var raw = await SendPromptAsync(prompt.ToString(), cancellationToken).ConfigureAwait(false);

                if (string.IsNullOrWhiteSpace(raw))
                {
                    return null;
                }

                var cleaned = CleanModelOutput(raw);

                return string.IsNullOrWhiteSpace(cleaned) ? null : cleaned;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                _logger.LogInformation("SummarizeReportAsync was cancelled by caller.");
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SummarizeReportAsync failed.");
                return null;
            }
        }

        public async Task<string?> ChatAsync(string message, string? context = null, CancellationToken cancellationToken = default)
        {
            try
            {
                var prompt = new StringBuilder();
                prompt.AppendLine("System: You are the Green City AI Assistant. Answer questions about the Green City Reporter application, reporting civic issues, tracking reports, categories, and statuses. Use only the information provided; do not access any databases.");
                if (!string.IsNullOrWhiteSpace(context))
                {
                    prompt.AppendLine();
                    prompt.AppendLine("Context:");
                    prompt.AppendLine(context);
                }

                prompt.AppendLine();
                prompt.AppendLine("User:");
                prompt.AppendLine(message ?? string.Empty);

                var raw = await SendPromptAsync(prompt.ToString(), cancellationToken).ConfigureAwait(false);

                if (string.IsNullOrWhiteSpace(raw))
                {
                    return null;
                }

                var cleaned = CleanModelOutput(raw);
                return string.IsNullOrWhiteSpace(cleaned) ? null : cleaned;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                _logger.LogInformation("ChatAsync was cancelled by caller.");
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ChatAsync failed.");
                return null;
            }
        }

        public async IAsyncEnumerable<string> ChatStreamAsync(
            string message,
            string? context = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var prompt = new StringBuilder();
            prompt.AppendLine("System: You are the Green City AI Assistant. Answer questions about Green City Reporter concisely and practically. Use only the supplied information.");
            if (!string.IsNullOrWhiteSpace(context))
            {
                prompt.AppendLine();
                prompt.AppendLine("Context:");
                prompt.AppendLine(context);
            }
            prompt.AppendLine();
            prompt.AppendLine("User:");
            prompt.AppendLine(message ?? string.Empty);

            var payload = new
            {
                model = _options.Ollama.Model,
                prompt = prompt.ToString(),
                stream = true,
                keep_alive = _options.Ollama.KeepAlive,
                options = new { temperature = 0, num_predict = _options.Ollama.NumPredict, num_ctx = _options.Ollama.NumCtx }
            };

            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/generate")
            {
                Content = new StringContent(JsonSerializer.Serialize(payload, _jsonOptions), Encoding.UTF8, "application/json")
            };
            using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode) yield break;

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            using var reader = new System.IO.StreamReader(stream);
            while (!cancellationToken.IsCancellationRequested)
            {
                var line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
                if (line == null) break;
                if (string.IsNullOrWhiteSpace(line)) continue;
                string? text = null;
                try
                {
                    using var doc = JsonDocument.Parse(line);
                    if (doc.RootElement.TryGetProperty("response", out var part) && part.ValueKind == JsonValueKind.String)
                        text = part.GetString();
                }
                catch (JsonException) { }
                if (!string.IsNullOrEmpty(text)) yield return text;
            }
        }

        private async Task<string?> SendPromptAsync(string prompt, CancellationToken cancellationToken)
        {
            try
            {
                var requestPayload = new
                {
                    model = _options.Ollama.Model,
                    prompt = prompt,
                    stream = false,
                    keep_alive = _options.Ollama.KeepAlive,
                    options = new
                    {
                        temperature = 0,
                        num_predict = _options.Ollama.NumPredict,
                        num_ctx = _options.Ollama.NumCtx
                    }
                };

                var content = new StringContent(JsonSerializer.Serialize(requestPayload, _jsonOptions), Encoding.UTF8, "application/json");

                using var response = await _httpClient.PostAsync("/api/generate", content, cancellationToken).ConfigureAwait(false);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Ollama returned non-success status code {StatusCode} when sending prompt.", response.StatusCode);
                    return null;
                }

                var responseText = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

                if (string.IsNullOrWhiteSpace(responseText))
                {
                    _logger.LogWarning("Ollama returned an empty response body.");
                    return null;
                }

                try
                {
                    using var doc = JsonDocument.Parse(responseText);
                    if (doc.RootElement.TryGetProperty("response", out var responseProperty) &&
                        responseProperty.ValueKind == JsonValueKind.String)
                    {
                        var cleanedResult = CleanModelOutput(responseProperty.GetString() ?? string.Empty);
                        _logger.LogInformation("AI returned cleaned result: {AIResult}", cleanedResult);
                        return string.IsNullOrWhiteSpace(cleanedResult) ? null : cleanedResult;
                    }

                    _logger.LogWarning("Ollama returned JSON without a usable response property.");
                    return null;
                }
                catch (JsonException)
                {
                    var cleanedResult = CleanModelOutput(responseText);
                    _logger.LogInformation("AI returned cleaned result: {AIResult}", cleanedResult);
                    return string.IsNullOrWhiteSpace(cleanedResult) ? null : cleanedResult;
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // Caller cancelled - not an error for logging as exception
                _logger.LogInformation("SendPromptAsync cancelled by caller.");
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while communicating with Ollama.");
                return null;
            }
        }

        private static string CleanModelOutput(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return raw ?? string.Empty;

            var s = raw.Trim();

            // Remove wrapping quotes if AI accidentally returns a quoted string
            if ((s.StartsWith("\"") && s.EndsWith("\"")) || (s.StartsWith("'") && s.EndsWith("'")))
            {
                s = s.Substring(1, s.Length - 2).Trim();
            }

            // Remove any leading/trailing newlines
            s = s.Trim();

            return s;
        }
    }
}
