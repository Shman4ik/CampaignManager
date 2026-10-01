using CampaignManager.Core.Catalogs;
using CampaignManager.Core.Characters;

namespace CampaignManager.Core.Tests.Parsers;

/// <summary>
/// Числа оружия на листе — разбор текста при чтении. Перенесено из T0.2: ступени «разобранное поле →
/// строка» в листе 2.0 нет, поэтому тест <c>ParsedFieldWinsOverText</c> v1 стал тестом того, что правка
/// текста сразу меняет число (в v1 бой бросал устаревший урон).
/// </summary>
[Trait("page", "399-402")]
public sealed class WeaponStatsReaderTests
{
    [Fact]
    public void NullWeapon_EverythingEmpty()
    {
        Assert.Null(WeaponStatsReader.AmmoCapacity(null));
        Assert.Null(WeaponStatsReader.BaseRangeMeters(null));
        Assert.False(WeaponStatsReader.TryMalfunctionThreshold(null, out var threshold));
        Assert.Equal(0, threshold);
        var range = WeaponStatsReader.Range(null);
        Assert.Equal((WeaponRangeKind.None, true), (range.Kind, range.IsParsed));
        Assert.Equal("", WeaponStatsReader.AttacksDisplay((SheetWeapon?)null));
        Assert.Null(WeaponStatsReader.MaxShotsPerRound(null));
    }

    [Fact]
    public void EditedText_ChangesNumbersImmediately()
    {
        var weapon = new SheetWeapon { Ammo = "6", Range = "15 метров", Attacks = "1", Damage = "1d8" };
        Assert.Equal(6, WeaponStatsReader.AmmoCapacity(weapon));
        Assert.Equal(8, WeaponStatsReader.Damage(weapon).GetDefaultDamage()!.Max);

        weapon.Ammo = "8";
        weapon.Range = "20 метров";
        weapon.Attacks = "1 (3)";
        weapon.Damage = "1d10+2";

        Assert.Equal(8, WeaponStatsReader.AmmoCapacity(weapon));
        Assert.Equal(20, WeaponStatsReader.BaseRangeMeters(weapon));
        Assert.Equal(3, WeaponStatsReader.MaxShotsPerRound(weapon));
        Assert.Equal(12, WeaponStatsReader.Damage(weapon).GetDefaultDamage()!.Max);
    }

    [Fact]
    public void TextOnly_ParsedOnTheFly()
    {
        var weapon = new SheetWeapon { Ammo = "6", Range = "15 метров", Attacks = "1 (3)", Malfunction = "00" };

        Assert.Equal(6, WeaponStatsReader.AmmoCapacity(weapon));
        Assert.Equal(15, WeaponStatsReader.BaseRangeMeters(weapon));
        Assert.Equal(1, WeaponStatsReader.ShotsPerRound(weapon));
        Assert.True(WeaponStatsReader.TryMalfunctionThreshold(weapon, out var threshold));
        Assert.Equal(100, threshold);
    }

    [Theory]
    [Trait("page", "113")]
    [InlineData("96", true, 96)]
    [InlineData("100", true, 100)]
    [InlineData("0", false, 0)]
    [InlineData("", false, 0)]
    [InlineData("101", false, 0)]
    public void TryMalfunctionThreshold_FromText(string text, bool has, int expected)
    {
        Assert.Equal(has, WeaponStatsReader.TryMalfunctionThreshold(new SheetWeapon { Malfunction = text }, out var threshold));
        Assert.Equal(expected, threshold);
    }

    [Theory]
    [InlineData("Автоподача", null)]
    [InlineData("Отдельно", null)]
    [InlineData("Только 1", 1)]
    [InlineData("20/30/32", 20)]
    [InlineData("", null)]
    public void AmmoCapacity_FirstMagazine_NoneForBelt(string ammo, int? expected) =>
        Assert.Equal(expected, WeaponStatsReader.AmmoCapacity(new SheetWeapon { Ammo = ammo }));

    [Theory]
    [InlineData("СИЛ / 5 метров", null)]
    [InlineData("Касание", null)]
    [InlineData("10/20/50 метров", 10)]
    [InlineData("Варьирует", null)]
    public void BaseRangeMeters_OnlyForMeasuredRange(string range, int? expected) =>
        Assert.Equal(expected, WeaponStatsReader.BaseRangeMeters(new SheetWeapon { Range = range }));

    [Theory]
    [InlineData("1 (3)", "1 (3)")]
    [InlineData("1 (1)", "1")]
    [InlineData("1/4", "1/4")]
    [InlineData("1 или очередями по 3", "1 · очередь по 3")]
    [InlineData("1 (2) или непр. огонь", "1 (2) · непр. огонь")]
    [InlineData("Непр. огонь", "непр. огонь")]
    [InlineData("Одноразовый", "1 · однораз.")]
    [InlineData("Нет", "Нет")]
    [InlineData("abc", "abc")]
    public void AttacksDisplay_SummaryOrRawText(string attacks, string expected) =>
        Assert.Equal(expected, WeaponStatsReader.AttacksDisplay(new SheetWeapon { Attacks = attacks }));

    [Theory]
    [InlineData("Касание", "касание")]
    [InlineData("На месте", "на месте")]
    [InlineData("СИЛ/5м", "СИЛ / 5 м")]
    [InlineData("10/20/50 метров", "10/20/50 м")]
    [InlineData("15 метров", "15 м")]
    [InlineData("Нет", "Нет")]
    [InlineData("abc", "abc")]
    public void RangeDisplay_SummaryOrRawText(string range, string expected) =>
        Assert.Equal(expected, WeaponStatsReader.RangeDisplay(new SheetWeapon { Range = range }));

    [Fact]
    public void MaxShotsPerRound_FallsBackToShots_NullForFullAutoOnly()
    {
        Assert.Equal(2, WeaponStatsReader.MaxShotsPerRound(new SheetWeapon { Attacks = "2" }));
        Assert.Null(WeaponStatsReader.MaxShotsPerRound(new SheetWeapon { Attacks = "Непр. огонь" }));
    }
}
