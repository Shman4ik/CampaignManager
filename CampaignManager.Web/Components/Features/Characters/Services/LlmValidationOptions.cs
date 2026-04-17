namespace CampaignManager.Web.Components.Features.Characters.Services;

public sealed class LlmValidationOptions
{
    public const string SectionName = "LlmValidation";
    public bool Enabled { get; set; }
    public string OllamaEndpoint { get; set; } = "http://localhost:11434";
    public string Model { get; set; } = "gemma4";
    public int MaxOutputTokensValidate { get; set; } = 8000;
    public float TemperatureValidate { get; set; } = 0.3f;
    public float TemperatureApply { get; set; } = 0.1f;
    public int TimeoutSeconds { get; set; } = 600;
}
