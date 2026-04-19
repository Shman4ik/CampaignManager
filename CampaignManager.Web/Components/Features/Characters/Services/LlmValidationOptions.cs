namespace CampaignManager.Web.Components.Features.Characters.Services;

public sealed class LlmValidationOptions
{
    public const string SectionName = "LlmValidation";
    public int MaxOutputTokensValidate { get; set; } = 16384;
    public float TemperatureValidate { get; set; } = 1.0f;
    public float TemperatureApply { get; set; } = 0.1f;
    public float TopP { get; set; } = 0.95f;
    public bool EnableThinking { get; set; } = true;
    public int ReasoningBudget { get; set; } = 16384;
}
