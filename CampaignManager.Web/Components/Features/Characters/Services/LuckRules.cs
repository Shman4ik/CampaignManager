using CampaignManager.Web.Components.Features.Combat.Model;
using CampaignManager.Web.Components.Features.Combat.Services;

namespace CampaignManager.Web.Components.Features.Characters.Services;

/// <summary>
///     Необязательное правило «Пункты Удачи» («Зов Ктулху» 7e, стр. 96) — <b>единственное</b>
///     место, где живёт его арифметика.
///     <para>
///         Один пункт Удачи уменьшает выпавшее число на единицу; потраченные пункты вычитаются
///         из текущей Удачи и сами по себе не восстанавливаются (восстановление — в фазе развития,
///         см. <see cref="DevelopmentPhaseRules" />). Потратить можно сколько угодно, но не больше
///         текущего значения Удачи, и только на собственный бросок.
///     </para>
///     <para>
///         Чего правило не разрешает и что поэтому не предлагает <see cref="Options" />:
///         проверки Удачи, урона, Рассудка и броски потери рассудка; повторную проверку
///         (игрок выбирает: либо повтор, либо Удача); крах, осечку и критический успех —
///         они вступают в силу в любом случае.
///     </para>
///     <para>
///         Успех, купленный за Удачу, не даёт права на улучшение навыка — галочку ставить нельзя,
///         поэтому потратившая сторона обязана её снять (<see cref="Model.Skill.IsUsed" />).
///     </para>
/// </summary>
public static class LuckRules
{
    /// <summary>
    ///     Во что обойдётся нужный уровень успеха: <paramref name="Cost" /> пунктов Удачи
    ///     превращают выпавшее число в <paramref name="ResultingRoll" />.
    /// </summary>
    public sealed record SpendOption(SuccessLevel Level, int Cost, int ResultingRoll, bool Affordable);

    /// <summary>Уровень успеха броска против значения навыка или характеристики (стр. 82).</summary>
    public static SuccessLevel LevelOf(int roll, int target) =>
        CombatService.CalculateSuccessLevel(roll, target);

    /// <summary>
    ///     Можно ли вообще торговаться за этот бросок. Крах, осечка и критический успех
    ///     действуют как выпали, а бросок вне 1–100 — не бросок.
    /// </summary>
    public static bool CanSpendOn(int roll, int target)
    {
        if (roll is < 1 or > 100 || target < 1)
            return false;

        var level = LevelOf(roll, target);
        return level is not (SuccessLevel.Fumble or SuccessLevel.CriticalSuccess);
    }

    /// <summary>
    ///     Уровни успеха, до которых этот бросок можно дотянуть Удачей, от дешёвого к дорогому.
    ///     Уже достигнутые уровни в список не попадают: за них платить нечем и незачем.
    ///     Слишком дорогие остаются в списке с <c>Affordable = false</c> — игрок должен видеть
    ///     цену, которую не потянул, иначе выбор выглядит произволом.
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
    ///     Наибольшее число, которое ещё считается успехом нужного уровня (стр. 82).
    ///     Половина и пятая часть — с округлением вниз, как на листе.
    /// </summary>
    private static IEnumerable<(SuccessLevel Level, int Threshold)> Thresholds(int target)
    {
        yield return (SuccessLevel.RegularSuccess, target);
        yield return (SuccessLevel.HardSuccess, target / 2);
        yield return (SuccessLevel.ExtremeSuccess, target / 5);
    }

    /// <summary>Как называется уровень успеха на листе.</summary>
    public static string LevelText(SuccessLevel level) => level switch
    {
        SuccessLevel.CriticalSuccess => "критический успех",
        SuccessLevel.ExtremeSuccess => "чрезвычайный успех",
        SuccessLevel.HardSuccess => "трудный успех",
        SuccessLevel.RegularSuccess => "обычный успех",
        SuccessLevel.Fumble => "крах",
        _ => "провал"
    };
}
