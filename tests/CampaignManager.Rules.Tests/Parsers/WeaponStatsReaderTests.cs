using CampaignManager.Web.Components.Features.Weapons.Model;
using CampaignManager.Web.Components.Features.Weapons.Services;
using CampaignManager.Web.Utilities.Services;

namespace CampaignManager.Rules.Tests.Parsers;

/// <summary>Порядок чтения числа у оружия: разобранное поле → разбор строки на лету → пусто.</summary>
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
        Assert.Equal("", WeaponStatsReader.AttacksDisplay(null));
        Assert.Null(WeaponStatsReader.MaxShotsPerRound(null));
    }

    [Fact]
    public void ParsedFieldWinsOverText()
    {
        var weapon = new Weapon
        {
            Ammo = "6",
            AmmoInfo = WeaponStatsParser.ParseAmmo("8"),
            Range = "15 метров",
            RangeInfo = WeaponStatsParser.ParseRange("20 метров"),
            Attacks = "1",
            AttacksInfo = WeaponStatsParser.ParseAttacks("1 (3)")
        };

        Assert.Equal(8, WeaponStatsReader.AmmoCapacity(weapon));
        Assert.Equal(20, WeaponStatsReader.BaseRangeMeters(weapon));
        Assert.Equal(3, WeaponStatsReader.MaxShotsPerRound(weapon));
    }

    [Fact]
    public void OldSheetCopy_TextOnly_ParsedOnTheFly()
    {
        var weapon = new Weapon { Ammo = "6", Range = "15 метров", Attacks = "1 (3)", Malfunction = "00" };

        Assert.Equal(6, WeaponStatsReader.AmmoCapacity(weapon));
        Assert.Equal(15, WeaponStatsReader.BaseRangeMeters(weapon));
        Assert.Equal(1, WeaponStatsReader.ShotsPerRound(weapon));
        Assert.True(WeaponStatsReader.TryMalfunctionThreshold(weapon, out var threshold));
        Assert.Equal(100, threshold);
    }

    [Theory]
    [Trait("page", "113")]
    [InlineData(96, "", true, 96)]
    [InlineData(null, "100", true, 100)]
    [InlineData(null, "0", false, 0)]
    [InlineData(null, "", false, 0)]
    // разобранное поле вне 1–100 строку не подменяет: осечки просто нет
    [InlineData(0, "100", false, 0)]
    [InlineData(101, "", false, 0)]
    public void TryMalfunctionThreshold_FieldThenText(int? field, string text, bool has, int expected)
    {
        var weapon = new Weapon { MalfunctionThreshold = field, Malfunction = text };

        Assert.Equal(has, WeaponStatsReader.TryMalfunctionThreshold(weapon, out var threshold));
        Assert.Equal(expected, threshold);
    }

    [Theory]
    [InlineData("Автоподача", null)]
    [InlineData("Отдельно", null)]
    [InlineData("Только 1", 1)]
    [InlineData("20/30/32", 20)]
    [InlineData("", null)]
    public void AmmoCapacity_FirstMagazine_NoneForBelt(string ammo, int? expected) =>
        Assert.Equal(expected, WeaponStatsReader.AmmoCapacity(new Weapon { Ammo = ammo }));

    [Theory]
    [InlineData("СИЛ / 5 метров", null)]
    [InlineData("Касание", null)]
    [InlineData("10/20/50 метров", 10)]
    [InlineData("Варьирует", null)]
    public void BaseRangeMeters_OnlyForMeasuredRange(string range, int? expected) =>
        Assert.Equal(expected, WeaponStatsReader.BaseRangeMeters(new Weapon { Range = range }));

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
        Assert.Equal(expected, WeaponStatsReader.AttacksDisplay(new Weapon { Attacks = attacks }));

    [Theory]
    [InlineData("Касание", "касание")]
    [InlineData("На месте", "на месте")]
    [InlineData("СИЛ/5м", "СИЛ / 5 м")]
    [InlineData("10/20/50 метров", "10/20/50 м")]
    [InlineData("15 метров", "15 м")]
    [InlineData("Нет", "Нет")]
    [InlineData("abc", "abc")]
    public void RangeDisplay_SummaryOrRawText(string range, string expected) =>
        Assert.Equal(expected, WeaponStatsReader.RangeDisplay(new Weapon { Range = range }));

    [Fact]
    public void MaxShotsPerRound_FallsBackToShots_NullForFullAutoOnly()
    {
        Assert.Equal(2, WeaponStatsReader.MaxShotsPerRound(new Weapon { Attacks = "2" }));
        Assert.Null(WeaponStatsReader.MaxShotsPerRound(new Weapon { Attacks = "Непр. огонь" }));
    }
}
