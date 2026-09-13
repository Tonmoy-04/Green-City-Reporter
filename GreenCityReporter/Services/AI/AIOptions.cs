using System;

namespace GreenCityReporter.Services.AI
{
    public class AIOptions
    {
        public string Provider { get; set; } = "Ollama";
        public int TimeoutSeconds { get; set; } = 30;
        public OllamaOptions Ollama { get; set; } = new OllamaOptions();
    }

    public class OllamaOptions
    {
        public string BaseUrl { get; set; } = "http://localhost:11434";
        public string Model { get; set; } = "llama3.2";
    }
}
