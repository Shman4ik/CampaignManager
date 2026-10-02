using CampaignManager.Core.Catalogs;

namespace CampaignManager.Core.Characters;

/// <summary>
/// Привыкание к ужасному (гл. 8, «Привыкание к ужасному», стр. 167): за встречи с одним видом тварей сыщик
/// теряет не больше, чем максимум провальной части его потери рассудка («0/1d6» → 6), а фаза развития снижает
/// накопленное на 1 (<see cref="DevelopmentPhaseRules.RelaxHabituations"/>). В v1 добавление записи и предел
/// жили в разметке панели; теперь здесь, а списание рассудка — по-прежнему только <see cref="SanityRules.ApplyLoss"/>.
/// </summary>
public static class HabituationRules
{
    public const int MaxLossLimit = 99;

    /// <summary>
    /// Запись о виде тварей. Тот же вид (по имени без учёта регистра) второй счётчик не заводит — только
    /// уточняет ссылку на бестиарий, запись потери и предел. Предел — из записи потери
    /// (<see cref="SanityLossFormula.MaxLoss"/>); без неё — 0, его впишет Хранитель.
    /// </summary>
    public static MythosHabituation Add(CharacterSheet sheet, string creatureName, Guid? creatureId = null, string? sanityLoss = null)
    {
        var name = creatureName.Trim();
        var formula = string.IsNullOrWhiteSpace(sanityLoss) ? null : sanityLoss.Trim();
        var maxLoss = formula is null ? 0 : SanityLossFormula.MaxLoss(formula);

        var existing = sheet.Condition.Habituations
            .FirstOrDefault(h => string.Equals(h.CreatureName.Trim(), name, StringComparison.CurrentCultureIgnoreCase));
        if (existing is not null)
        {
            existing.CreatureId ??= creatureId;
            existing.SanityLossFormula ??= formula;
            if (maxLoss > 0)
                existing.MaxLoss = maxLoss;
            return existing;
        }

        var created = new MythosHabituation
        {
            CreatureId = creatureId,
            CreatureName = name,
            MaxLoss = maxLoss,
            SanityLossFormula = formula,
        };
        sheet.Condition.Habituations.Add(created);
        return created;
    }

    /// <summary>Предел, вписанный руками: 0…99.</summary>
    public static void SetMaxLoss(MythosHabituation habituation, int maxLoss) =>
        habituation.MaxLoss = Math.Clamp(maxLoss, 0, MaxLossLimit);

    /// <summary>
    /// Потеря рассудка от встречи с этим видом: не больше остатка до предела (за предел этот вид рассудка уже
    /// не отнимает), списывается одной причиной через <see cref="SanityRules.ApplyLoss"/>. Возвращает списанное.
    /// Без предела (0) ограничения нет.
    /// </summary>
    public static int Lose(CharacterSheet sheet, SkillCatalog catalog, MythosHabituation habituation, int amount)
    {
        var allowed = habituation.MaxLoss > 0 ? Math.Min(amount, habituation.Remaining) : amount;
        if (allowed <= 0)
            return 0;

        var lost = SanityRules.ApplyLoss(sheet, catalog, allowed);
        habituation.LostSanity += lost;
        return lost;
    }
}
