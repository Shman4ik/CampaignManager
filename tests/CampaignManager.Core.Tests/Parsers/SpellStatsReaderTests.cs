using CampaignManager.Core.Catalogs;

namespace CampaignManager.Core.Tests.Parsers;

/// <summary>
///     Стоимость и время сотворения из свободного текста заклинания. Это подсказка для боевой
///     панели, а не правило книги, поэтому страницы нет.
/// </summary>
[Trait("page", "?")]
public sealed class SpellStatsReaderTests
{
    [Theory]
    [InlineData("8 пунктов магии, 1d6 пунктов рассудка", "8", "1d6", null, null)]
    [InlineData("5 МОЩ", null, null, "5", null)]
    [InlineData("3 пункта магии, 1d8 пунктов рассудка, 1 ПЗ за раунд", "3", "1d8", null, "1")]
    [InlineData("10 ПМ", "10", null, null, null)]
    [InlineData("1d4+3 пункта магии", "1d4+3", null, null, null)]
    [InlineData("1d4 + 3 пункта магии", "1d4+3", null, null, null)]
    [InlineData("1D6 пунктов рассудка", null, "1d6", null, null)]
    // русская «д» между цифрами превращается в латинскую
    [InlineData("1д6 пунктов рассудка", null, "1d6", null, null)]
    [InlineData("1 д 10 рассудка", null, "1d10", null, null)]
    [InlineData("2 пункта здоровья", null, null, null, "2")]
    [InlineData("1 пункт МОЩ, 2d6 рассудка", null, "2d6", "1", null)]
    // «≥» выбрасывается
    [InlineData("≥5 пунктов магии", "5", null, null, null)]
    public void ParseCost_ByKind(string text, string? mp, string? sanity, string? power, string? hp)
    {
        var cost = SpellStatsReader.ParseCost(text);

        Assert.Equal((mp, sanity, power, hp), (cost.MagicPoints, cost.Sanity, cost.Power, cost.HitPoints));
        Assert.False(cost.IsEmpty);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("варьирует")]
    [InlineData("(Урон×2+1) магии за раунд")]
    public void ParseCost_Unreadable_Empty(string? text) =>
        Assert.True(SpellStatsReader.ParseCost(text).IsEmpty);

    [Theory]
    [InlineData("мгновенно", 0, null, true, false)]
    [InlineData("Мгновенное", 0, null, true, false)]
    [InlineData("5 раундов", 5, null, false, false)]
    [InlineData("1 раунд", 1, null, false, false)]
    [InlineData("1 или 2 раунда", 1, null, false, false)]
    [InlineData("1d3 раунда", null, "1d3", false, false)]
    [InlineData("1д3 раунда", null, "1d3", false, false)]
    // кости раньше числа: «4» в «1d6 + 4» не ловится как раунды
    [InlineData("1d6 + 4 раунда", null, "1d6+4", false, false)]
    [InlineData("1 час", null, null, false, true)]
    [InlineData("2 минуты", null, null, false, true)]
    [InlineData("1 раунд, но длится часами", 1, null, false, true)]
    [InlineData("", null, null, false, false)]
    [InlineData(null, null, null, false, false)]
    public void ParseCastingTime_Forms(string? text, int? rounds, string? formula, bool instant, bool longer) =>
        Assert.Equal(new SpellStatsReader.CastingTimeEstimate(rounds, formula, instant, longer),
            SpellStatsReader.ParseCastingTime(text));

    [Theory]
    [InlineData("8", 8)]
    [InlineData(" 8 ", 8)]
    [InlineData("0", 0)]
    [InlineData("1d6", null)]
    [InlineData("-1", null)]
    [InlineData(null, null)]
    public void FixedAmount_OnlyPlainNonNegativeNumber(string? formula, int? expected) =>
        Assert.Equal(expected, SpellStatsReader.FixedAmount(formula));
}
