namespace CampaignManager.Web.Components.Features.Characters.Services;

/// <summary>
///     Кости для правил листа персонажа. Отдельный тип, чтобы правила не тянули за собой
///     собственный <see cref="Random" /> и оставались читаемыми.
/// </summary>
public static class Dice
{
    /// <summary>Сумма <paramref name="count" />d<paramref name="sides" />.</summary>
    public static int Roll(int count, int sides)
    {
        var total = 0;
        for (var i = 0; i < count; i++)
            total += Random.Shared.Next(1, sides + 1);
        return total;
    }

    /// <summary>1d100: две кости десятков и единиц, «00 0» читается как 100.</summary>
    public static int Percentile()
    {
        var tens = Random.Shared.Next(0, 10);
        var units = Random.Shared.Next(0, 10);
        return Combine(tens, units);
    }

    /// <summary>
    ///     1d100 с бонусной костью: бросают две кости десятков и берут меньший результат (стр. 84).
    /// </summary>
    public static int PercentileWithBonusDie()
    {
        var units = Random.Shared.Next(0, 10);
        var first = Combine(Random.Shared.Next(0, 10), units);
        var second = Combine(Random.Shared.Next(0, 10), units);
        return Math.Min(first, second);
    }

    private static int Combine(int tens, int units)
    {
        var value = tens * 10 + units;
        return value == 0 ? 100 : value;
    }
}
