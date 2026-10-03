using CampaignManager.Core;
using CampaignManager.Core.Catalogs;
using CampaignManager.Core.Dice;
using Xunit;

namespace CampaignManager.Core.Tests.Dice;

/// <summary>Запись костей на экране — правило 13 дизайн-системы: <c>1d6 + 2</c>, <c>3d6 × 5</c>.</summary>
public sealed class DiceNotationTests
{
    [Theory]
    [InlineData("1D6", "1d6")]
    [InlineData("1d6+1", "1d6 + 1")]
    [InlineData("1d6 +1", "1d6 + 1")]
    [InlineData("1д6", "1d6")]
    [InlineData("3D6*5", "3d6 × 5")]
    [InlineData("3d6x5", "3d6 × 5")]
    [InlineData("3d6 × 5", "3d6 × 5")]
    [InlineData("1D6−1", "1d6 - 1")]
    [InlineData("1d8+1d4+2", "1d8 + 1d4 + 2")]
    [InlineData("d100", "d100")]
    [InlineData("1d6 + БкУ", "1d6 + БкУ")]
    [InlineData("1D4+Б.К.У.", "1d4 + Б.К.У.")]
    public void Format_normalises_a_roll(string input, string expected) =>
        Assert.Equal(expected, DiceNotation.Format(input));

    [Theory]
    [InlineData("Потеря 1D6 Рассудка", "Потеря 1d6 Рассудка")]
    [InlineData("урон 2D6+2, дальность 15 м", "урон 2d6 + 2, дальность 15 м")]
    [InlineData("2-3 м", "2-3 м")]
    [InlineData("Ad6", "Ad6")]
    [InlineData("", "")]
    public void Format_leaves_the_rest_of_the_text_alone(string input, string expected) =>
        Assert.Equal(expected, DiceNotation.Format(input));

    [Fact]
    public void Format_of_null_is_empty() => Assert.Equal("", DiceNotation.Format(null));

    [Fact]
    public void Format_is_idempotent() =>
        Assert.Equal("1d6 + 2", DiceNotation.Format(DiceNotation.Format("1D6+2")));
}

public sealed class TermsTests
{
    [Theory]
    [InlineData("1d4 + Б.К.У.", "1d4 + бонус к урону")]
    [InlineData("1d4+БкУ", "1d4+бонус к урону")]
    [InlineData("1d8 + 1/2БкУ", "1d8 + 1/2 бонус к урону")]
    [InlineData("Б. К. У.", "бонус к урону")]
    [InlineData("без изменений", "без изменений")]
    public void Normalize_replaces_the_damage_bonus_abbreviation(string input, string expected) =>
        Assert.Equal(expected, Terms.Normalize(input));

    [Fact]
    public void Empty_phrases_come_in_two_forms()
    {
        Assert.Equal("Нет книг.", Terms.Empty("книг"));
        Assert.Equal("Нет книг по этим условиям.", Terms.EmptyFiltered("книг"));
    }

    [Fact]
    public void Units_follow_the_dictionary()
    {
        Assert.Equal("100 м", Terms.Meters(100));
        Assert.Equal("$1000", Terms.Dollars(1000m));
        Assert.Equal("$12.5", Terms.Dollars(12.5m));
    }

    [Fact]
    public void Catalog_text_speaks_the_dictionary_not_the_abbreviation()
    {
        var attack = new CreatureAttack { Damage = "2D6", DamageBonusMode = CreatureDamageBonusMode.Full };

        Assert.Equal("2d6 + бонус к урону", CatalogText.Damage(attack));
        Assert.Equal("+ бонус к урону", CatalogText.Of(CreatureDamageBonusMode.Full));
        Assert.DoesNotContain("БкУ", CatalogText.Of(CreatureDamageBonusMode.OnlyBonus), StringComparison.Ordinal);
    }
}
