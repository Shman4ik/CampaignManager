using CampaignManager.Core;
using CampaignManager.Core.Dice;
using CampaignManager.Core.Encounters.Chase;

namespace CampaignManager.UI.Encounters.Chase;

/// <summary>
/// Поля одной проверки на панели погоны: что бросают, значение и бросок. Навык и значение помнятся между действиями
/// (помеха за помехой — та же Ловкость), бросок — нет. Проверку для правила собирает <see cref="ToCheck"/>.
/// </summary>
public sealed class ChaseCheckInput
{
    public string Key { get; set; } = ChaseSkills.Dex;

    public string Label { get; set; } = "ЛВК";

    public int? Value { get; set; }

    public D100Roll? Roll { get; set; }

    public bool IsReady => Value is > 0;

    public ChaseCheck ToCheck(Difficulty difficulty = Difficulty.Regular, int bonusDice = 0, int penaltyDice = 0) =>
        new(Label, Value ?? 0, Roll?.Result, difficulty, bonusDice, penaltyDice);

    /// <summary>Задать навык (код или характеристику) и его значение — при смене участника или действия.</summary>
    public void Set(string key, string label, int? value)
    {
        Key = key;
        Label = label;
        Value = value;
        Roll = null;
    }
}
