using System.Text;

namespace GreenCityReporter.Services.AI
{
    public static class AIClassificationPromptBuilder
    {
        public const string CategorySystemPrompt = "You are a civic issue category classifier for Green City Reporter. Choose exactly one supported category from the supplied list. Return only valid JSON with this exact shape: {\"category\":\"exact category name\",\"critical\":false,\"confidence\":0.9}. Do not include markdown or explanation.";

        public const string PrioritySystemPrompt = "You are a civic issue priority classifier for Green City Reporter. Analyze the supplied civic report and return exactly one value and nothing else: Low, Medium, High, or Critical.";

        public const string SummarySystemPrompt = "You write concise administrative summaries for civic reports. Return only a concise 1-2 sentence summary. Do not return JSON. Do not include markdown.";

        public const string ChatSystemPrompt = "You are the Green City AI Assistant. Answer only about Green City Reporter and the user's report-related questions, using the provided context when available. Keep responses concise and factual.";

        public static string BuildClassificationPrompt(string title, string description, IEnumerable<string> availableCategories)
        {
            var categories = (availableCategories ?? Enumerable.Empty<string>())
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(c => c, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var prompt = new StringBuilder();
            prompt.AppendLine("Classify this civic report using only the categories provided below.");
            prompt.AppendLine("Choose exactly one category from the supplied list.");
            prompt.AppendLine("Return only valid JSON with this exact shape:");
            prompt.AppendLine("{\"category\":\"exact category name\",\"critical\":false,\"confidence\":0.9}");
            prompt.AppendLine();
            prompt.AppendLine("Rules:");
            prompt.AppendLine("- The category must match one of the exact category names provided in the list.");
            prompt.AppendLine("- Use only supported categories; do not invent or rename categories.");
            prompt.AppendLine("- Set critical to true only for imminent danger, active fire, severe electrical or gas hazard, major flood, major road accident, or dangerous infrastructure failure.");
            prompt.AppendLine("- Confidence must be a number between 0 and 1.");
            prompt.AppendLine("- Return no markdown, no explanation, and no surrounding text.");
            prompt.AppendLine();
            prompt.AppendLine("Allowed categories:");
            foreach (var category in categories)
            {
                prompt.AppendLine($"- {category}");
            }

            prompt.AppendLine();
            prompt.AppendLine("Title:");
            prompt.AppendLine(title ?? string.Empty);
            prompt.AppendLine();
            prompt.AppendLine("Description:");
            prompt.AppendLine(description ?? string.Empty);

            return prompt.ToString();
        }

        public static string BuildPriorityPrompt(string title, string description)
        {
            var prompt = new StringBuilder();
            prompt.AppendLine("Analyze the civic report below and assign a priority.");
            prompt.AppendLine("Return exactly one of: Low, Medium, High, Critical");
            prompt.AppendLine("Do not return JSON, markdown, or explanation.");
            prompt.AppendLine();
            prompt.AppendLine("Title:");
            prompt.AppendLine(title ?? string.Empty);
            prompt.AppendLine();
            prompt.AppendLine("Description:");
            prompt.AppendLine(description ?? string.Empty);
            return prompt.ToString();
        }

        public static string BuildSummaryPrompt(string title, string description)
        {
            var prompt = new StringBuilder();
            prompt.AppendLine("Write a brief administrative summary of the civic issue below.");
            prompt.AppendLine("Return only a concise 1-2 sentence summary.");
            prompt.AppendLine("Do not return JSON, markdown, or commentary.");
            prompt.AppendLine();
            prompt.AppendLine("Title:");
            prompt.AppendLine(title ?? string.Empty);
            prompt.AppendLine();
            prompt.AppendLine("Description:");
            prompt.AppendLine(description ?? string.Empty);
            return prompt.ToString();
        }
    }
}
