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
    ///     Максимум Рассудка = 99 − значение навыка "Мифы Ктулху" (стр. 55).
    /// </summary>
    public static int ComputeMaxSanity(Character character)
    {
        var mythos = GetMythosValue(character);
        return Math.Max(0, AbsoluteMaxSanity - mythos);
    }

    public static int GetMythosValue(Character character)
    {
        foreach (var group in character.Skills.SkillGroups)
        foreach (var skill in group.Skills)
        {
            if (string.Equals(skill.Name, MythosSkillName, StringComparison.Ordinal))
                return skill.Value.Regular;
        }

        return 0;
    }

    /// <summary>
    ///     Порог для проверки на бессрочное безумие — 1/5 от текущего Рассудка (стр. 106).
    /// </summary>
    public static int IndefiniteInsanityThreshold(int currentSanity) => currentSanity / 5;

    /// <summary>
    ///     Триггер на проверку ИНТ → временное безумие (стр. 79).
    /// </summary>
    public const int TemporaryInsanityThreshold = 5;
}
