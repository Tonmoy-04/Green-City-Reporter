using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using GreenCityReporter.Models.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GreenCityReporter.Services.AI
{
    public class GroqAIService : IAIService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<GroqAIService> _logger;
        private readonly AIOptions _options;

        public GroqAIService(HttpClient httpClient, ILogger<GroqAIService> logger, IOptions<AIOptions> options)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _options = options?.Value ?? throw new ArgumentNullException(nameof(options));

            if (_httpClient.BaseAddress == null)
            {
                try
                {
                    _httpClient.BaseAddress = new Uri(_options.Groq.BaseUrl);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Invalid Groq BaseUrl in options: {BaseUrl}", _options.Groq.BaseUrl);
                }
            }
        }

        public async Task<ReportClassificationResult?> ClassifyReportAsync(
            string title,
            string description,
            IEnumerable<string> availableCategories,
            CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Groq classification request. Provider={Provider}, Model={Model}, BaseUrl={BaseUrl}, HasApiKey={HasApiKey}", _options.Provider, ResolveModel(), GetBaseUrl(), !string.IsNullOrWhiteSpace(_options.Groq.ApiKey));

            try
            {
                var categories = (availableCategories ?? Enumerable.Empty<string>())
                    .Where(c => !string.IsNullOrWhiteSpace(c))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                if (categories.Count == 0)
                {
                    return null;
                }

                var prompt = AIClassificationPromptBuilder.BuildClassificationPrompt(title, description, categories);
                var json = await SendChatCompletionAsync(
                    taskName: "Category",
                    systemPrompt: AIClassificationPromptBuilder.CategorySystemPrompt,
                    userPrompt: prompt,
                    cancellationToken: cancellationToken,
                    maxTokens: 250).ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(json))
                {
                    _logger.LogWarning("Groq response was empty. Falling back to the existing workflow.");
                    return null;
                }

                var parsed = ParseClassification(json, categories);
                if (parsed is null)
                {
                    _logger.LogWarning("Groq classification parsing failed. Falling back to the existing workflow.");
                }

                return parsed;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "ClassifyReportAsync could not parse the Groq AI response.");
                return null;
            }
        }

        public async Task<string?> CategorizeReportAsync(
            string title,
            string description,
            IEnumerable<string> availableCategories,
            CancellationToken cancellationToken = default)
        {
            var result = await ClassifyReportAsync(title, description, availableCategories, cancellationToken).ConfigureAwait(false);
            return result?.Category;
        }

        public async Task<Priority?> DetectPriorityAsync(string title, string description, CancellationToken cancellationToken = default)
        {
            try
            {
                var userPrompt = AIClassificationPromptBuilder.BuildPriorityPrompt(title, description);
                var raw = await SendChatCompletionAsync(
                    taskName: "Priority",
                    systemPrompt: AIClassificationPromptBuilder.PrioritySystemPrompt,
                    userPrompt: userPrompt,
                    cancellationToken: cancellationToken,
                    maxTokens: 50).ConfigureAwait(false);

                if (string.IsNullOrWhiteSpace(raw)) return null;

                var cleaned = CleanModelOutput(raw);
                if (Enum.TryParse<Priority>(cleaned, true, out var priority)) return priority;

                _logger.LogWarning("Groq returned invalid priority value '{PriorityValue}'. Falling back.", cleaned);
                return null;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "DetectPriorityAsync failed for Groq.");
                return null;
            }
        }

        public async Task<string?> SummarizeReportAsync(string title, string description, CancellationToken cancellationToken = default)
        {
            try
            {
                var userPrompt = AIClassificationPromptBuilder.BuildSummaryPrompt(title, description);
                var raw = await SendChatCompletionAsync(
                    taskName: "Summary",
                    systemPrompt: AIClassificationPromptBuilder.SummarySystemPrompt,
                    userPrompt: userPrompt,
                    cancellationToken: cancellationToken,
                    maxTokens: 200).ConfigureAwait(false);

                return string.IsNullOrWhiteSpace(raw) ? null : CleanModelOutput(raw);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "SummarizeReportAsync failed for Groq.");
                return null;
            }
        }

        public async Task<string?> ChatAsync(string message, string? context = null, CancellationToken cancellationToken = default)
        {
            try
            {
                var prompt = new StringBuilder();
                prompt.AppendLine(AIClassificationPromptBuilder.ChatSystemPrompt);
                if (!string.IsNullOrWhiteSpace(context))
                {
                    prompt.AppendLine();
                    prompt.AppendLine("Context:");
                    prompt.AppendLine(context);
                }

                prompt.AppendLine();
                prompt.AppendLine("User:");
                prompt.AppendLine(message ?? string.Empty);
                var raw = await SendChatCompletionAsync(
                    taskName: "Chat",
                    systemPrompt: AIClassificationPromptBuilder.ChatSystemPrompt,
                    userPrompt: prompt.ToString(),
                    cancellationToken: cancellationToken,
                    maxTokens: 250).ConfigureAwait(false);
                return string.IsNullOrWhiteSpace(raw) ? null : CleanModelOutput(raw);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "ChatAsync failed for Groq.");
                return null;
            }
        }

        public async IAsyncEnumerable<string> ChatStreamAsync(string message, string? context = null, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var prompt = new StringBuilder();
            prompt.AppendLine(AIClassificationPromptBuilder.ChatSystemPrompt);
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
                model = ResolveModel(),
                messages = new[]
                {
                    new { role = "system", content = AIClassificationPromptBuilder.ChatSystemPrompt },
                    new { role = "user", content = prompt.ToString() }
                },
                temperature = 0.1,
                stream = true
            };

            using var request = new HttpRequestMessage(HttpMethod.Post, "chat/completions")
            {
                Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.Groq.ApiKey);

            using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Groq chat stream returned status {StatusCode}.", response.StatusCode);
                yield break;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            using var reader = new System.IO.StreamReader(stream);

            while (!cancellationToken.IsCancellationRequested)
            {
                var line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
                if (line == null) break;
                if (string.IsNullOrWhiteSpace(line) || !line.StartsWith("data: ", StringComparison.Ordinal)) continue;

                var chunk = line[6..].Trim();
                if (chunk == "[DONE]") break;

                if (!TryParseStreamingChunk(chunk, out var text))
                {
                    continue;
                }

                if (!string.IsNullOrEmpty(text))
                {
                    yield return text;
                }
            }
        }

        private static bool TryParseStreamingChunk(string chunk, out string? text)
        {
            text = null;

            try
            {
                using var doc = JsonDocument.Parse(chunk);
                if (doc.RootElement.TryGetProperty("choices", out var choices) && choices.ValueKind == JsonValueKind.Array)
                {
                    foreach (var choice in choices.EnumerateArray())
                    {
                        if (choice.TryGetProperty("delta", out var delta) && delta.TryGetProperty("content", out var content) && content.ValueKind == JsonValueKind.String)
                        {
                            text = content.GetString();
                            return !string.IsNullOrEmpty(text);
                        }
                    }
                }
            }
            catch (JsonException)
            {
                // Ignore chunk parsing failures, since streaming tokens are best-effort.
            }

            return false;
        }

        private async Task<string?> SendChatCompletionAsync(
            string taskName,
            string systemPrompt,
            string userPrompt,
            CancellationToken cancellationToken,
            int maxTokens = 300)
        {
            if (string.IsNullOrWhiteSpace(_options.Groq.ApiKey))
            {
                _logger.LogWarning("Groq API key is not configured. AI fallback will be used.");
                return null;
            }

            var model = ResolveModel();
            var endpoint = GetChatCompletionsUri();
            var payload = new
            {
                model,
                messages = new[]
                {
                    new { role = "system", content = systemPrompt },
                    new { role = "user", content = userPrompt }
                },
                temperature = 0,
                max_tokens = maxTokens
            };

            _logger.LogInformation("Groq task {TaskName}: provider={Provider}, model={Model}, endpoint={Endpoint}, hasApiKey={HasApiKey}", taskName, _options.Provider, model, endpoint, !string.IsNullOrWhiteSpace(_options.Groq.ApiKey));

            using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
            {
                Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.Groq.ApiKey);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            try
            {
                using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
                var responseText = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

                _logger.LogInformation("Groq task {TaskName} HTTP status: {StatusCode}", taskName, response.StatusCode);
                if (!string.IsNullOrWhiteSpace(responseText) && responseText.Length <= 4096)
                {
                    _logger.LogDebug("Groq task {TaskName} raw response: {ResponseBody}", taskName, responseText);
                }

                if (response.StatusCode == HttpStatusCode.TooManyRequests)
                {
                    _logger.LogWarning("Groq task {TaskName} rate limit reached. Response: {ResponseBody}", taskName, responseText);
                    return null;
                }

                if (response.StatusCode == HttpStatusCode.Unauthorized || response.StatusCode == HttpStatusCode.Forbidden)
                {
                    _logger.LogWarning("Groq task {TaskName} authentication failed. Status: {StatusCode}. Response: {ResponseBody}", taskName, response.StatusCode, responseText);
                    return null;
                }

                if ((int)response.StatusCode >= 500)
                {
                    _logger.LogWarning("Groq task {TaskName} server error. Status: {StatusCode}. Response: {ResponseBody}", taskName, response.StatusCode, responseText);
                    return null;
                }

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Groq task {TaskName} non-success status {StatusCode}. Response: {ResponseBody}", taskName, response.StatusCode, responseText);
                    return null;
                }

                if (string.IsNullOrWhiteSpace(responseText))
                {
                    _logger.LogWarning("Groq task {TaskName} returned an empty response body.", taskName);
                    return null;
                }

                using var doc = JsonDocument.Parse(responseText);
                if (!doc.RootElement.TryGetProperty("choices", out var choices) || choices.ValueKind != JsonValueKind.Array || choices.GetArrayLength() == 0)
                {
                    _logger.LogWarning("Groq task {TaskName} returned no valid completion choices. Response body was present but unusable.", taskName);
                    return null;
                }

                var firstChoice = choices[0];
                if (!firstChoice.TryGetProperty("finish_reason", out var finishReason))
                {
                    finishReason = default;
                }

                if (firstChoice.TryGetProperty("message", out var message) && message.TryGetProperty("content", out var content) && content.ValueKind == JsonValueKind.String)
                {
                    var contentText = content.GetString() ?? string.Empty;
                    var cleaned = CleanModelOutput(contentText);
                    _logger.LogInformation("Groq task {TaskName}: content present={HasContent}, finish_reason={FinishReason}", taskName, !string.IsNullOrWhiteSpace(cleaned), finishReason.ValueKind == JsonValueKind.String ? finishReason.GetString() : finishReason.ToString());
                    return string.IsNullOrWhiteSpace(cleaned) ? null : cleaned;
                }

                _logger.LogWarning("Groq task {TaskName} returned no content in message.content. Finish reason: {FinishReason}", taskName, finishReason.ValueKind == JsonValueKind.String ? finishReason.GetString() : finishReason.ToString());
                return null;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                _logger.LogInformation("Groq task {TaskName} cancelled by caller.", taskName);
                return null;
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Groq task {TaskName} returned malformed JSON. Parsing exception: {Message}", taskName, ex.Message);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Groq task {TaskName} communication error. Message: {Message}", taskName, ex.Message);
                return null;
            }
        }

        private ReportClassificationResult? ParseClassification(string content, IReadOnlyCollection<string> availableCategories)
        {
            var raw = CleanModelOutput(content);
            if (string.IsNullOrWhiteSpace(raw))
            {
                _logger.LogWarning("Groq classification content was empty after cleaning.");
                return null;
            }

            try
            {
                var cleaned = ExtractFirstJsonObject(raw);
                if (string.IsNullOrWhiteSpace(cleaned))
                {
                    _logger.LogWarning("Groq classification content did not contain a JSON object after extraction. Raw content: {Content}", raw);
                    return null;
                }

                using var doc = JsonDocument.Parse(cleaned);
                var root = doc.RootElement;

                var category = root.TryGetProperty("category", out var categoryElement) && categoryElement.ValueKind == JsonValueKind.String
                    ? categoryElement.GetString()
                    : null;

                var critical = root.TryGetProperty("critical", out var criticalElement) && criticalElement.ValueKind == JsonValueKind.True;

                var confidenceValue = root.TryGetProperty("confidence", out var confidenceElement) && confidenceElement.TryGetDouble(out var confidence)
                    ? Math.Clamp(confidence, 0d, 1d)
                    : (double?)null;

                if (string.IsNullOrWhiteSpace(category) || confidenceValue is null)
                {
                    _logger.LogWarning("Groq classification JSON is missing category or confidence. JSON: {Json}", cleaned);
                    return null;
                }

                var matchedCategory = availableCategories.FirstOrDefault(candidate => string.Equals(candidate, category, StringComparison.OrdinalIgnoreCase));
                if (matchedCategory == null)
                {
                    _logger.LogWarning("Groq returned unsupported category '{Category}'. Supported categories: {AvailableCategories}", category, string.Join(", ", availableCategories));
                    return null;
                }

                return new ReportClassificationResult(matchedCategory, critical, confidenceValue.Value);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Groq classification JSON could not be parsed. Raw content: {Content}", raw);
                return null;
            }
        }

        private static string ExtractFirstJsonObject(string raw)
        {
            var text = raw.Trim();
            if (string.IsNullOrWhiteSpace(text)) return string.Empty;

            var fenceStart = text.IndexOf("```", StringComparison.OrdinalIgnoreCase);
            if (fenceStart >= 0)
            {
                var fenceEnd = text.IndexOf("```", fenceStart + 3, StringComparison.OrdinalIgnoreCase);
                if (fenceEnd > fenceStart)
                {
                    text = text.Substring(fenceStart + 3, fenceEnd - fenceStart - 3).Trim();
                }
            }

            var jsonStart = text.IndexOf('{');
            var jsonEnd = text.LastIndexOf('}');
            if (jsonStart >= 0 && jsonEnd > jsonStart)
            {
                return text.Substring(jsonStart, jsonEnd - jsonStart + 1).Trim();
            }

            var trimmed = text.Trim();
            if (trimmed.StartsWith("json", StringComparison.OrdinalIgnoreCase))
            {
                trimmed = trimmed.Substring(4).Trim();
            }

            return trimmed;
        }

        private string ResolveModel()
        {
            var model = _options.Groq.Model;
            if (string.IsNullOrWhiteSpace(model))
            {
                return "openai/gpt-oss-20b";
            }

            return model.Trim();
        }

        private string GetBaseUrl()
        {
            return string.IsNullOrWhiteSpace(_options.Groq.BaseUrl)
                ? "https://api.groq.com/openai/v1/"
                : _options.Groq.BaseUrl.Trim();
        }

        private string GetChatCompletionsUri()
        {
            var baseUrl = GetBaseUrl();
            return new Uri(new Uri(baseUrl.EndsWith('/') ? baseUrl : baseUrl + "/"), "chat/completions").ToString();
        }

        private static string CleanModelOutput(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return string.Empty;

            var text = raw.Trim();
            if ((text.StartsWith("\"") && text.EndsWith("\"")) || (text.StartsWith("'") && text.EndsWith("'")))
            {
                text = text.Substring(1, text.Length - 2).Trim();
            }

            while (text.StartsWith("```", StringComparison.OrdinalIgnoreCase))
            {
                text = text.Substring(3).Trim();
            }

            while (text.EndsWith("```", StringComparison.OrdinalIgnoreCase))
            {
                text = text.Substring(0, text.Length - 3).Trim();
            }

            return text.Trim();
        }
    }
}
