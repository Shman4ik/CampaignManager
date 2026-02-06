namespace CampaignManager.Web.Components.Features.Combat.Model;

/// <summary>
/// DTO для настройки проверки рассудка
/// </summary>
public class SanityCheckSetup
{
    public Guid TargetId { get; set; }
    public int CurrentSanity { get; set; }

    /// <summary>
    /// Потеря рассудка при успехе (напр. "0", "1", "1D3")
    /// </summary>
    public string SuccessLoss { get; set; } = "0";

    /// <summary>
    /// Потеря рассудка при неудаче (напр. "1D6", "1D10", "2D6")
    /// </summary>
    public string FailureLoss { get; set; } = "1D6";
}
