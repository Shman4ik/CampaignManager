using CampaignManager.Web.Components.Features.Combat.Services;

namespace CampaignManager.Web.Components.Features.Characters.Services;

/// <summary>
///     Кости для правил листа персонажа — короткая запись, чтобы правила оставались читаемыми.
///     <para>
///         Своих бросков здесь нет: бросает <see cref="CombatService" />. Бросок d100 с бонусными
///         и штрафными костями на приложение один — <see cref="CombatService.RollD100(int, int)" />
///         (стр. 89); раньше здесь жила своя копия бонусной кости, и две реализации одного правила
///         рано или поздно разошлись бы.
///     </para>
/// </summary>
public static class Dice
{
    /// <summary>Сумма <paramref name="count" />d<paramref name="sides" />.</summary>
    public static int Roll(int count, int sides)
    {
        var total = 0;
        for (var i = 0; i < count; i++)
            total += CombatService.RollDice(sides);
        return total;
    }

    /// <summary>
    ///     1d100; с <paramref name="bonusDice" /> бонусными костями бросают несколько костей десятков
    ///     и берут меньший результат (стр. 89).
    /// </summary>
    public static int Percentile(int bonusDice = 0) => CombatService.RollD100(bonusDice, 0).Result;
}
