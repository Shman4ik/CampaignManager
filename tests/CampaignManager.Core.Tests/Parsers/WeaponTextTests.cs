using CampaignManager.Core.Catalogs;
using Xunit;

namespace CampaignManager.Core.Tests.Parsers;

/// <summary>
/// Показ строк оружия и цен (F6a, W3, I2): «Нет», «метр», «Только 1», «СИЛ/5м» — одним написанием, «$1000» и «$20 000»
/// без разрыва посреди числа. Хранимое не меняется — только то, что видит человек.
/// </summary>
public sealed class WeaponTextTests
{
    [Theory]
    [InlineData("Нет", null)]
    [InlineData("нет", null)]
    [InlineData("  ", null)]
    [InlineData("—", null)]
    [InlineData(null, null)]
    [InlineData("15 м", "15 м")]
    [InlineData("10/20/50 м", "10/20/50 м")]
    [InlineData("Касание", "Касание")]
    [InlineData("1 метр", "1 м")]
    [InlineData("3 метра", "3 м")]
    [InlineData("15м", "15 м")]
    [InlineData("СИЛ / 5 м", "СИЛ / 5 м")]
    [InlineData("СИЛ/5м", "СИЛ / 5 м")]
    [InlineData("Сил/5м", "СИЛ / 5 м")]
    public void Range_has_one_spelling_for_nothing_metres_and_strength(string? raw, string? expected) =>
        Assert.Equal(expected, WeaponText.Range(raw));

    [Theory]
    [InlineData("2d10 / 1 метр", "2d10 / 1 м")]
    [InlineData("4d10 / 3 метра", "4d10 / 3 м")]
    [InlineData("1d6 + 2", "1d6 + 2")]
    [InlineData("", null)]
    public void Damage_uses_metres_like_range(string raw, string? expected) =>
        Assert.Equal(expected, WeaponText.Damage(raw));

    [Theory]
    [InlineData("Только 1", "1 (одноразовое)")]
    [InlineData("Одноразовая", "1 (одноразовое)")]
    [InlineData("Однораз.", "1 (одноразовое)")]
    [InlineData("Одноразовый", "1 (одноразовое)")]
    [InlineData("20/30/32", "20/30/32")]
    [InlineData("25 доз", "25 доз")]
    [InlineData("Минимум 10", "Минимум 10")]
    [InlineData("", null)]
    [InlineData("Нет", null)]
    public void Ammo_names_one_shot_the_same_way(string raw, string? expected) =>
        Assert.Equal(expected, WeaponText.Ammo(raw));

    [Fact]
    public void Attacks_drop_nothing_but_keep_the_text()
    {
        Assert.Null(WeaponText.Attacks("Нет"));
        Assert.Equal("1 (2) или непр. огонь", WeaponText.Attacks("1 (2) или непр. огонь"));
    }

    [Theory]
    [InlineData("Нет", null)]
    [InlineData("$1000/20 000", "$1000/20\u202F000")]
    [InlineData("$3000/50 000", "$3000/50\u202F000")]
    [InlineData("$1/20 коробка", "$1/20 коробка")]
    [InlineData("$0,05/0,50", "$0,05/0,50")]
    [InlineData("от $200 / $1600", "от $200 / $1600")]
    [InlineData("$9 000", "$9000")]
    [InlineData("$20000", "$20\u202F000")]
    public void Cost_groups_thousands_only_from_five_digits(string raw, string? expected) =>
        Assert.Equal(expected, WeaponText.Cost(raw));

    [Theory]
    [InlineData(9, "$9")]
    [InlineData(1000, "$1000")]
    [InlineData(9000, "$9000")]
    [InlineData(20000, "$20\u202F000")]
    [InlineData(1250000, "$1\u202F250\u202F000")]
    public void Price_is_whole_dollars_without_a_break_inside_the_number(int dollars, string expected) =>
        Assert.Equal(expected, PriceText.Format((decimal)dollars));

    [Fact]
    public void Price_keeps_cents_with_a_comma()
    {
        Assert.Equal("$6,40", PriceText.Format(6.4m));
        Assert.Equal("$0,05", PriceText.Format(0.05m));
        Assert.Null(PriceText.Format((decimal?)null));
    }
}
