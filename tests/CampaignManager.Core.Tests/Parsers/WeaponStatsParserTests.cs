using System.Text.Encodings.Web;
using System.Text.Json;
using CampaignManager.Core.Catalogs;

namespace CampaignManager.Core.Tests.Parsers;

/// <summary>Колонки «Дальность», «Атак», «Боезапас», «Стоимость» и «Осечка» таблицы XVII.</summary>
[Trait("page", "399-402")]
public sealed class WeaponStatsParserTests
{
    private static readonly JsonSerializerOptions Json = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    /// <summary>Сравнить разобранный объект с JSON из каталога: оба прогоняются через один сериализатор.</summary>
    private static void AssertSameJson<T>(string expectedJson, T actual)
    {
        var expected = JsonSerializer.Deserialize<T>(expectedJson, Json);
        Assert.Equal(JsonSerializer.Serialize(expected, Json), JsonSerializer.Serialize(actual, Json));
    }

    // ── Все значения каталога ────────────────────────────────────────

    [Theory]
    [MemberData(nameof(WeaponCatalogRows.Range), MemberType = typeof(WeaponCatalogRows))]
    public void ParseRange_EveryCatalogValue_MatchesBackfill(string raw, string expectedJson) =>
        AssertSameJson(expectedJson, WeaponStatsParser.ParseRange(raw));

    [Theory]
    [MemberData(nameof(WeaponCatalogRows.Attacks), MemberType = typeof(WeaponCatalogRows))]
    public void ParseAttacks_EveryCatalogValue_MatchesBackfill(string raw, string expectedJson) =>
        AssertSameJson(expectedJson, WeaponStatsParser.ParseAttacks(raw));

    [Theory]
    [MemberData(nameof(WeaponCatalogRows.Ammo), MemberType = typeof(WeaponCatalogRows))]
    public void ParseAmmo_EveryCatalogValue_MatchesBackfill(string raw, string expectedJson) =>
        AssertSameJson(expectedJson, WeaponStatsParser.ParseAmmo(raw));

    [Theory]
    [MemberData(nameof(WeaponCatalogRows.Cost), MemberType = typeof(WeaponCatalogRows))]
    public void ParseCost_EveryCatalogValue_MatchesBackfill(string raw, string expectedJson) =>
        AssertSameJson(expectedJson, WeaponStatsParser.ParseCost(raw));

    /// <summary>Разобранные числа каталога осмысленны: ничего отрицательного и нулевого там, где должно быть число.</summary>
    [Fact]
    public void Catalog_ParsedNumbersArePositive()
    {
        foreach (var row in WeaponCatalogRows.Range)
        {
            var r = WeaponStatsParser.ParseRange(row.Data.Item1);
            Assert.True(r.IsParsed, r.RawText);
            Assert.True(r.BaseMeters is null or > 0, r.RawText);
            Assert.True(r.ThrowDivisor is null or > 0, r.RawText);
        }

        foreach (var row in WeaponCatalogRows.Attacks)
        {
            var a = WeaponStatsParser.ParseAttacks(row.Data.Item1);
            Assert.True(a.IsParsed, a.RawText);
            Assert.True(a.ShotsPerRound is null or >= 1, a.RawText);
            Assert.True(a.RoundsPerAttack is null or (>= 2 and <= 4), a.RawText);
        }

        // «Варьирует» — единственное значение каталога, которое честно остаётся текстом
        var ammo = WeaponCatalogRows.Ammo.Select(row => WeaponStatsParser.ParseAmmo(row.Data.Item1)).ToList();
        Assert.Equal(["Варьирует"], ammo.Where(a => !a.IsParsed).Select(a => a.RawText));
        Assert.All(ammo, a => Assert.True(a.Capacity is null or > 0, a.RawText));

        foreach (var row in WeaponCatalogRows.Cost)
        {
            var c = WeaponStatsParser.ParseCost(row.Data.Item1);
            Assert.True(c.IsParsed, c.RawText);
            Assert.True(c.Cost1920 is null or > 0 && c.CostModern is null or > 0, c.RawText);
        }
    }

    // ── Дальность ────────────────────────────────────────────────────

    [Theory]
    [InlineData("", WeaponRangeKind.None, null, true)]
    [InlineData(null, WeaponRangeKind.None, null, true)]
    [InlineData("—", WeaponRangeKind.None, null, true)]
    [InlineData("-", WeaponRangeKind.None, null, true)]
    [InlineData("нет", WeaponRangeKind.None, null, true)]
    [InlineData("контакт", WeaponRangeKind.Touch, null, true)]
    [InlineData("Ближний бой", WeaponRangeKind.Touch, null, true)]
    [InlineData("15 м", WeaponRangeKind.Meters, 15, true)]
    [InlineData("20", WeaponRangeKind.Meters, 20, true)]
    [InlineData("5 / 10 метров", WeaponRangeKind.RangeBands, 5, true)]
    [InlineData("сил / 3", WeaponRangeKind.StrengthThrow, null, true)]
    // делитель 0 — не метательное, и числа в начале нет
    [InlineData("СИЛ / 0 метров", WeaponRangeKind.None, null, false)]
    [InlineData("Варьирует", WeaponRangeKind.None, null, false)]
    [InlineData("около 15 метров", WeaponRangeKind.None, null, false)]
    public void ParseRange_Forms(string? raw, WeaponRangeKind kind, int? meters, bool parsed)
    {
        var r = WeaponStatsParser.ParseRange(raw);

        Assert.Equal((kind, meters, parsed), (r.Kind, r.BaseMeters, r.IsParsed));
        Assert.Equal(raw ?? "", r.RawText);
    }

    // ── Атаки ────────────────────────────────────────────────────────

    [Theory]
    [InlineData("3", 3, null, null, true)]
    [InlineData("1(3)", 1, 3, null, true)]
    [InlineData("1/20", 1, null, 20, true)]
    // «1/25» и «1/0» — опечатка: выстрелы записаны, раунды нет, строка не разобрана
    [InlineData("1/25", 1, null, null, false)]
    [InlineData("1/0", 1, null, null, false)]
    [InlineData("Однораз.", 1, null, null, true)]
    [InlineData("abc", null, null, null, false)]
    [InlineData("", null, null, null, true)]
    public void ParseAttacks_Forms(string raw, int? shots, int? max, int? rounds, bool parsed)
    {
        var a = WeaponStatsParser.ParseAttacks(raw);

        Assert.Equal((shots, max, rounds, parsed), (a.ShotsPerRound, a.MaxShotsPerRound, a.RoundsPerAttack, a.IsParsed));
    }

    [Fact]
    public void ParseAttacks_BurstAndFullAutoTogether()
    {
        var a = WeaponStatsParser.ParseAttacks("1 (2), очередями по 5 или непр. огонь");

        Assert.Equal((1, 2), (a.ShotsPerRound, a.MaxShotsPerRound));
        Assert.Equal((true, 5, true), (a.AllowsBurst, a.BurstSize, a.AllowsFullAuto));
    }

    // ── Боезапас ─────────────────────────────────────────────────────

    [Theory]
    [InlineData("Автоподача, 250", null, true)]
    [InlineData("6 (револьвер)", 6, true)]
    [InlineData("abc", null, false)]
    public void ParseAmmo_Forms(string raw, int? capacity, bool parsed)
    {
        var a = WeaponStatsParser.ParseAmmo(raw);

        Assert.Equal(parsed, a.IsParsed);
        if (raw.StartsWith("Автоподача"))
            Assert.Equal((true, 250), (a.IsBeltFed, a.Capacity));
        else
            Assert.Equal(capacity, a.Capacity);
    }

    // ── Стоимость ────────────────────────────────────────────────────

    [Theory]
    [InlineData("65¢", "0.65", null, true)]
    [InlineData("$25/65¢", "25", "0.65", true)]
    [InlineData("от $200", "200", null, true)]
    [InlineData("редкое", null, null, false)]
    [InlineData("", null, null, true)]
    public void ParseCost_Forms(string raw, string? cost1920, string? costModern, bool parsed)
    {
        var c = WeaponStatsParser.ParseCost(raw);

        Assert.Equal(cost1920 is null ? null : (decimal?)decimal.Parse(cost1920, System.Globalization.CultureInfo.InvariantCulture), c.Cost1920);
        Assert.Equal(costModern is null ? null : (decimal?)decimal.Parse(costModern, System.Globalization.CultureInfo.InvariantCulture), c.CostModern);
        Assert.Equal(parsed, c.IsParsed);
    }

    [Fact]
    public void ParseCost_SinglePrice_ModernSilent()
    {
        var c = WeaponStatsParser.ParseCost("$200");

        Assert.Null(c.CostModern);
        Assert.False(c.UnavailableModern);
        Assert.False(c.Unavailable1920);
    }

    // ── Осечка (стр. 113) ────────────────────────────────────────────

    [Theory]
    [Trait("page", "113")]
    [InlineData("100", 100)]
    [InlineData("00", 100)]
    [InlineData("000", 100)]
    [InlineData("96", 96)]
    [InlineData("1", 1)]
    [InlineData(" 98 ", 98)]
    [InlineData("99+", 99)]
    // одиночный «0» — незаполненное поле, а не «клинит всегда»
    [InlineData("0", null)]
    [InlineData("101", null)]
    [InlineData("", null)]
    [InlineData("  ", null)]
    [InlineData(null, null)]
    [InlineData("нет", null)]
    // все цифры склеиваются: «96–100» — это 96100, вне диапазона
    [InlineData("96–100", null)]
    [InlineData("9 7", 97)]
    public void ParseMalfunction_Forms(string? raw, int? expected) =>
        Assert.Equal(expected, WeaponStatsParser.ParseMalfunction(raw));

    /// <summary>Все пороги осечки, которые миграция приведения каталога записала по таблице XVII.</summary>
    [Theory]
    [Trait("page", "113")]
    [InlineData("93")]
    [InlineData("95")]
    [InlineData("96")]
    [InlineData("97")]
    [InlineData("98")]
    [InlineData("99")]
    [InlineData("100")]
    public void ParseMalfunction_CatalogThresholds_AsNumbers(string raw) =>
        Assert.Equal(int.Parse(raw), WeaponStatsParser.ParseMalfunction(raw));
}
