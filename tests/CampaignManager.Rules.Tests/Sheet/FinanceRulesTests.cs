using CampaignManager.Web.Components.Features.Characters.Services;

namespace CampaignManager.Rules.Tests.Sheet;

/// <summary>Таблица II «Наличные и активы», раздел «Достаток» и разбор денег из строки.</summary>
public sealed class FinanceRulesTests
{
    /// <summary>Столбец «1920-е». Активы −1 означают «нет».</summary>
    [Theory]
    [Trait("page", "45")]
    [InlineData(-1, "Нищий", 0.5, -1, 0.5)]
    [InlineData(0, "Нищий", 0.5, -1, 0.5)]
    [InlineData(1, "Бедный", 1, 10, 2)]
    [InlineData(9, "Бедный", 9, 90, 2)]
    [InlineData(10, "Среднего класса", 20, 500, 10)]
    [InlineData(49, "Среднего класса", 98, 2450, 10)]
    [InlineData(50, "Состоятельный", 250, 25000, 50)]
    [InlineData(89, "Состоятельный", 445, 44500, 50)]
    [InlineData(90, "Богатый", 1800, 180000, 250)]
    [InlineData(98, "Богатый", 1960, 196000, 250)]
    [InlineData(99, "Сверхбогатый", 50000, 5000000, 5000)]
    [InlineData(100, "Сверхбогатый", 50000, 5000000, 5000)]
    public void GetTier_Classic_AllBoundaries(int cr, string name, double cash, double assets, double pocket) =>
        AssertTier(FinanceRules.GetTier(cr, isModern: false), name, cash, assets, pocket, cr >= 99);

    /// <summary>Столбец «наше время».</summary>
    [Theory]
    [Trait("page", "45")]
    [InlineData(-1, "Нищий", 10, -1, 10)]
    [InlineData(0, "Нищий", 10, -1, 10)]
    [InlineData(1, "Бедный", 20, 200, 40)]
    [InlineData(9, "Бедный", 180, 1800, 40)]
    [InlineData(10, "Среднего класса", 400, 10000, 200)]
    [InlineData(49, "Среднего класса", 1960, 49000, 200)]
    [InlineData(50, "Состоятельный", 5000, 500000, 1000)]
    [InlineData(89, "Состоятельный", 8900, 890000, 1000)]
    [InlineData(90, "Богатый", 36000, 3600000, 5000)]
    [InlineData(98, "Богатый", 39200, 3920000, 5000)]
    [InlineData(99, "Сверхбогатый", 1000000, 100000000, 100000)]
    [InlineData(100, "Сверхбогатый", 1000000, 100000000, 100000)]
    public void GetTier_Modern_AllBoundaries(int cr, string name, double cash, double assets, double pocket) =>
        AssertTier(FinanceRules.GetTier(cr, isModern: true), name, cash, assets, pocket, cr >= 99);

    [Fact]
    [Trait("page", "45")]
    public void WealthTier_Texts()
    {
        var penniless = FinanceRules.GetTier(0, isModern: false);
        Assert.Equal("0.50", penniless.CashText);
        Assert.Equal("0.50", penniless.PocketMoneyText);
        Assert.Equal("нет", penniless.AssetsText);

        var superRich = FinanceRules.GetTier(99, isModern: false);
        Assert.Equal("50000", superRich.CashText);
        Assert.Equal("5000000+", superRich.AssetsText);

        Assert.Equal("500", FinanceRules.GetTier(10, isModern: false).AssetsText);
    }

    [Theory]
    [Trait("page", "44")]
    [InlineData(0, -1)]
    [InlineData(1, 9)]
    [InlineData(10, 49)]
    [InlineData(50, 89)]
    [InlineData(90, 98)]
    [InlineData(99, 150)]
    public void GetLifestyle_SameRowsAsTableTwo(int from, int to)
    {
        Assert.Equal(FinanceRules.GetLifestyle(from), FinanceRules.GetLifestyle(to));
        if (from > 0)
            Assert.NotEqual(FinanceRules.GetLifestyle(from - 1), FinanceRules.GetLifestyle(from));
    }

    [Theory]
    [Trait("page", "94")]
    [InlineData("$148", 148)]
    [InlineData("148", 148)]
    [InlineData("$148.50", 148.5)]
    [InlineData("148,50 долларов", 148.5)]
    [InlineData("$0.50", 0.5)]
    [InlineData("1 234", 1234)]
    [InlineData("£20", 20)]
    [InlineData("20 ₽", 20)]
    [InlineData("€12,5", 12.5)]
    [InlineData("10к", 10)]
    public void TryParseMoney_ExtractsDigits(string text, double expected) =>
        Assert.Equal((decimal)expected, FinanceRules.TryParseMoney(text));

    [Theory]
    [Trait("page", "94")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("нет")]
    [InlineData("$")]
    [InlineData(".")]
    [InlineData("1.234.56")]
    [InlineData("1,234.56")]
    public void TryParseMoney_TextOrAmbiguous_ReturnsNull(string? text) =>
        Assert.Null(FinanceRules.TryParseMoney(text));

    /// <summary>
    ///     Разбор просто выбрасывает всё, кроме цифр, точки и запятой: запятая-разделитель тысяч
    ///     становится десятичной, минус теряется, дробь склеивается в число.
    /// </summary>
    [Theory]
    [Trait("page", "94")]
    [Trait("finding", "F-S06")]
    [InlineData("$1,500", 1.5)]
    [InlineData("-50", 50)]
    [InlineData("1/2", 12)]
    public void TryParseMoney_Quirks(string text, double expected) =>
        Assert.Equal((decimal)expected, FinanceRules.TryParseMoney(text));

    [Theory]
    [Trait("page", "45")]
    [InlineData(148, "148")]
    [InlineData(0.5, "0.50")]
    [InlineData(1.234, "1.23")]
    [InlineData(1000000, "1000000")]
    public void Format_IntegersWithoutCents(double value, string expected) =>
        Assert.Equal(expected, FinanceRules.Format((decimal)value));

    private static void AssertTier(
        FinanceRules.WealthTier tier, string name, double cash, double assets, double pocket, bool assetsAreMinimum)
    {
        Assert.Equal(name, tier.Name);
        Assert.Equal((decimal)cash, tier.Cash);
        Assert.Equal(assets < 0 ? null : (decimal)assets, tier.Assets);
        Assert.Equal((decimal)pocket, tier.PocketMoney);
        Assert.Equal(assetsAreMinimum, tier.AssetsAreMinimum);
    }
}
