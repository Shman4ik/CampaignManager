using CampaignManager.Core.Dice;

namespace CampaignManager.Core.Characters;

/// <summary>
/// Необязательное правило «Пункты Удачи» (стр. 97) — <b>единственное</b> место его арифметики. В v1 было
/// две траты Удачи с противоположной логикой (диалог листа снимал отметку развития, диалог проверки — нет);
/// теперь обе идут через <see cref="Spend"/>.
/// <para>
/// Пункт Удачи уменьшает выпавшее на единицу; потраченное вычитается из текущей Удачи и само не
/// восстанавливается (только в фазе развития). Нельзя: проверки Удачи, урона, Рассудка, повторную
/// проверку; крах, осечку и критический успех. Успех, купленный за Удачу, не даёт отметки развития.
/// </para>
/// </summary>
public static class LuckRules
{
    /// <summary>Во что обойдётся уровень: <paramref name="Cost"/> пунктов превращают бросок в <paramref name="ResultingRoll"/>.</summary>
    public sealed record SpendOption(SuccessLevel Level, int Cost, int ResultingRoll, bool Affordable);

    /// <summary>Уровень броска против значения (стр. 82) — через общие пороги <see cref="Check"/>.</summary>
    public static SuccessLevel LevelOf(int roll, int target) => Check.Evaluate(roll, target);

    /// <summary>Можно ли торговаться за бросок: крах и критический успех действуют как выпали, вне 1–100 — не бросок.</summary>
    public static bool CanSpendOn(int roll, int target)
    {
        if (roll is < 1 or > 100 || target < 1)
            return false;

        return LevelOf(roll, target) is not (SuccessLevel.Fumble or SuccessLevel.Critical);
    }

    /// <summary>
    /// Уровни, до которых бросок можно дотянуть, от дешёвого к дорогому. Достигнутые не попадают; слишком
    /// дорогие остаются с <c>Affordable = false</c> — игрок должен видеть цену, которую не потянул.
    /// </summary>
    public static IReadOnlyList<SpendOption> Options(int roll, int target, int currentLuck)
    {
        if (!CanSpendOn(roll, target))
            return [];

        var current = LevelOf(roll, target);
        List<SpendOption> options = [];

        foreach (var (level, threshold) in Thresholds(target))
        {
            if (level <= current || threshold < 1)
                continue;

            var cost = roll - threshold;
            if (cost < 1)
                continue;

            options.Add(new SpendOption(level, cost, threshold, cost <= currentLuck));
        }

        return options;
    }

    /// <summary>
    /// Тратит Удачу на собственный бросок: списывает пункты и снимает отметку развития с навыка, если он
    /// задан — купленный успех её не даёт (стр. 97). Возвращает false, если Удачи не хватает.
    /// </summary>
    public static bool Spend(CharacterSheet sheet, int cost, SheetSkill? skill = null)
    {
        if (cost < 1 || cost > sheet.Current.Luck)
            return false;

        sheet.Current.Luck -= cost;
        if (skill is not null)
            skill.Checked = false;
        return true;
    }

    /// <summary>Наибольшее число, ещё дающее уровень (стр. 82); половина и пятая — вниз, как на бланке.</summary>
    private static IEnumerable<(SuccessLevel Level, int Threshold)> Thresholds(int target)
    {
        yield return (SuccessLevel.Regular, target);
        yield return (SuccessLevel.Hard, target / 2);
        yield return (SuccessLevel.Extreme, target / 5);
    }
}
