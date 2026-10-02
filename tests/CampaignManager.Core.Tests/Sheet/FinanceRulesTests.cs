using CampaignManager.Core.Characters;

namespace CampaignManager.Core.Tests.Sheet;

/// <summary>
/// Таблица II «Наличные и активы» и раздел «Достаток». Перенесено из T0.2 без правки ожиданий. Тестов
/// <c>TryParseMoney</c> здесь нет: в листе 2.0 деньги — числа, разбор строки v1 (с причудами F-S06)
/// нужен только переносу (T1.3), и его тесты переезжают туда.
/// </summary>
public sealed class FinanceRulesTests
{
    /// <summary>Столбец «1920-е». Активы −1 — «нет».</summary>
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
        AssertTier(FinanceRules.GetTier(cr, Era.Classic), name, cash, assets, pocket, cr >= 99);

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
        AssertTier(FinanceRules.GetTier(cr, Era.Modern), name, cash, assets, pocket, cr >= 99);

    [Fact]
    [Trait("page", "45")]
    public void WealthTier_Texts()
    {
        var penniless = FinanceRules.GetTier(0, Era.Classic);
        Assert.Equal("0.50", penniless.CashText);
        Assert.Equal("0.50", penniless.PocketMoneyText);
        Assert.Equal("нет", penniless.AssetsText);

        var superRich = FinanceRules.GetTier(99, Era.Classic);
        Assert.Equal("50000", superRich.CashText);
        Assert.Equal("5000000+", superRich.AssetsText);

        Assert.Equal("500", FinanceRules.GetTier(10, Era.Classic).AssetsText);
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
    [Trait("page", "45")]
    [InlineData(148, "148")]
    [InlineData(0.5, "0.50")]
    [InlineData(1.234, "1.23")]
    [InlineData(1000000, "1000000")]
    public void Format_IntegersWithoutCents(double value, string expected) =>
        Assert.Equal(expected, FinanceRules.Format((decimal)value));

    [Theory]
    [Trait("page", "45")]
    [InlineData(0, Era.Classic, 0.5, 0.5, "нет")]
    [InlineData(20, Era.Classic, 40, 10, "1000")]
    [InlineData(99, Era.Modern, 1000000, 100000, "100000000+")]
    public void ForNewInvestigator_NumbersAndAssetsText(int cr, Era era, double cash, double pocket, string assets)
    {
        var finances = FinanceRules.ForNewInvestigator(cr, era);

        Assert.Equal((decimal)cash, finances.Cash);
        Assert.Equal((decimal)pocket, finances.PocketMoney);
        Assert.Equal(assets, finances.Assets);
    }

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
