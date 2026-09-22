using System;

namespace GreenCityReporter.Services.AI
{
    public class AIOptions
    {
        public string Provider { get; set; } = "Ollama";
        public int TimeoutSeconds { get; set; } = 30;
        public OllamaOptions Ollama { get; set; } = new OllamaOptions();
        public GroqOptions Groq { get; set; } = new GroqOptions();
    }

    public class OllamaOptions
    {
        public string BaseUrl { get; set; } = "http://localhost:11434";
        public string Model { get; set; } = "llama3.2";
        // Keep the model resident between requests to avoid repeated cold starts.
        public string KeepAlive { get; set; } = "10m";
        // AI answers in this app are intentionally concise; limiting output reduces latency.
        public int NumPredict { get; set; } = 160;
        public int NumCtx { get; set; } = 2048;
    }

    public class GroqOptions
    {
        public string BaseUrl { get; set; } = "https://api.groq.com/openai/v1/";
        public string Model { get; set; } = "openai/gpt-oss-20b";
        public string ApiKey { get; set; } = "";
    }
}
