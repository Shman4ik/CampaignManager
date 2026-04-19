namespace CampaignManager.Web.Utilities.Services;

public sealed class LlmOptions
{
    public const string SectionName = "Llm";
    public bool Enabled { get; set; }
    public string Provider { get; set; } = "Ollama";

    // Ollama
    public string OllamaEndpoint { get; set; } = "http://localhost:11434";
    public string OllamaModel { get; set; } = "gemma4";

    // NVIDIA NIM
    public string NvidiaEndpoint { get; set; } = "https://integrate.api.nvidia.com";
    public string NvidiaModel { get; set; } = "meta/llama-3.3-70b-instruct";
    public string NvidiaApiKey { get; set; } = "";

    public int TimeoutSeconds { get; set; } = 600;

    public bool IsNvidia => Provider.Equals("Nvidia", StringComparison.OrdinalIgnoreCase);
}
