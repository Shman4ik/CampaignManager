namespace CampaignManager.Web.Components.Features.Combat.Model;

/// <summary>
/// DTO для настройки проверки рассудка (CoC 7e, глава 8)
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

    /// <summary>
    /// Существо, при виде которого идёт проверка. Нужно для привыкания к ужасному
    /// (стр. 167): потеря за этот вид складывается в общий счётчик на листе, а сверх
    /// предела сыщик за него уже не теряет.
    /// </summary>
    public Guid? SourceCreatureId { get; set; }

    /// <summary>Название вида тварей для записи привыкания.</summary>
    public string? SourceCreatureName { get; set; }

    /// <summary>Запись потери из бестиария как есть («0/1d6») — задаёт предел привыкания.</summary>
    public string? SourceSanityLossFormula { get; set; }

    /// <summary>
    /// Ручной ввод броска d100 (null = авторбросок).
    /// Бонусные и штрафные кости к проверкам Рассудка не применяются (стр. 152).
    /// </summary>
    public int? ManualRoll { get; set; }

    /// <summary>
    /// Ручной ввод броска ИНТ при потере 5+ пунктов за раз (null = авторбросок).
    /// </summary>
    public int? ManualIntRoll { get; set; }

    /// <summary>
    /// Ручной бросок 1d10 на длительность временного безумия в часах (null = авторбросок).
    /// </summary>
    public int? ManualInsanityDurationRoll { get; set; }
}
