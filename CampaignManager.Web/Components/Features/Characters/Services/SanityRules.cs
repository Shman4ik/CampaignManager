using CampaignManager.Web.Components.Features.Characters.Model;

namespace CampaignManager.Web.Components.Features.Characters.Services;

/// <summary>
///     Правила Рассудка из главы 8 ("Зов Ктулху" 7e).
/// </summary>
public static class SanityRules
{
    public const int AbsoluteMaxSanity = 99;
    private const string MythosSkillName = "Мифы Ктулху";

    /// <summary>
    ///     Максимум Рассудка = 99 − значение навыка "Мифы Ктулху" (стр. 63 и 152).
    /// </summary>
    public static int ComputeMaxSanity(Character character)
    {
        var mythos = GetMythosValue(character);
        return Math.Max(0, AbsoluteMaxSanity - mythos);
    }

    public static int GetMythosValue(Character character) => FindMythosSkill(character)?.Value.Regular ?? 0;

    /// <summary>
    ///     Порог для проверки на бессрочное безумие — 1/5 от текущего Рассудка, потерянные
    ///     за один игровой день (стр. 153).
    /// </summary>
    public static int IndefiniteInsanityThreshold(int currentSanity) => currentSanity / 5;

    /// <summary>
    ///     Триггер на проверку ИНТ → временное безумие: 5 и более пунктов, потерянных
    ///     по одной и той же причине (стр. 152).
    /// </summary>
    public const int TemporaryInsanityThreshold = 5;

    /// <summary>
    ///     Рассудок упал до нуля — сыщик неизлечимо безумен и выбывает из игры (стр. 153).
    ///     Отдельного флага не держим: это ровно "Рассудок = 0".
    /// </summary>
    public static bool IsPermanentlyInsane(Character character) =>
        character.DerivedAttributes.Sanity.Value <= 0;

    /// <summary>
    ///     Записывает случай безумия, связанного с Мифами: первый даёт +5 к навыку "Мифы Ктулху",
    ///     каждый следующий +1 (стр. 160–161). Максимум Рассудка при этом падает.
    /// </summary>
    public static int RecordMythosInsanity(Character character)
    {
        var gain = character.State.MythosInsanityCount == 0 ? 5 : 1;
        character.State.MythosInsanityCount++;

        var mythos = FindMythosSkill(character);
        if (mythos is not null)
        {
            mythos.Value.Regular = Math.Min(99, mythos.Value.Regular + gain);
            mythos.Value.UpdateDerived();
        }

        // Новый максимум может оказаться ниже текущего Рассудка — прижимаем.
        var max = ComputeMaxSanity(character);
        character.DerivedAttributes.Sanity.MaxValue = max;
        character.DerivedAttributes.Sanity.Value = Math.Min(character.DerivedAttributes.Sanity.Value, max);

        return gain;
    }

    private static Skill? FindMythosSkill(Character character) =>
        character.Skills.SkillGroups
            .SelectMany(g => g.Skills)
            .FirstOrDefault(s => string.Equals(s.Name, MythosSkillName, StringComparison.Ordinal));
}
