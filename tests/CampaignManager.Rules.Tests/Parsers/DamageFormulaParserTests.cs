using CampaignManager.Rules.Tests.Infrastructure;
using CampaignManager.Web.Components.Features.Combat.Services;
using CampaignManager.Web.Model;
using CampaignManager.Web.Utilities.Services;

namespace CampaignManager.Rules.Tests.Parsers;

/// <summary>Колонка «Урон» таблицы XVII: формулы костей, бонус к урону, дробовики, взрывчатка.</summary>
[Trait("page", "399-402")]
public sealed class DamageFormulaParserTests
{
    /// <summary>Кости формулы строкой «NdX», через запятую — для сравнения одной строкой.</summary>
    private static string Dice(DamageExpression? e) =>
        e is null ? "" : string.Join(",", e.Dice.Select(d => d.ToString()));

    [Theory]
    [InlineData("1d6", "1d6", 0)]
    [InlineData("1D6", "1d6", 0)]
    [InlineData("1д6", "1d6", 0)]
    [InlineData("1Д6", "1d6", 0)]
    [InlineData("d6", "1d6", 0)]
    [InlineData("2d6+4", "2d6", 4)]
    [InlineData("2d6 + 4", "2d6", 4)]
    [InlineData(" 2d6 +4 ", "2d6", 4)]
    [InlineData("1d6-1", "1d6", -1)]
    [InlineData("1d6 - 1", "1d6", -1)]
    [InlineData("1d10+1d4+2", "1d10,1d4", 2)]
    [InlineData("1d8 + 1d4 + 2", "1d8,1d4", 2)]
    [InlineData("1d6-1d4", "1d6,-1d4", 0)]
    [InlineData("2d6+2+1", "2d6", 3)]
    [InlineData("+1d4", "1d4", 0)]
    [InlineData("-1", "", -1)]
    public void Parse_DiceAndFlatModifier(string raw, string dice, int flat)
    {
        var info = DamageFormulaParser.Parse(raw);

        Assert.True(info.IsParsed);
        Assert.NotNull(info.Primary);
        Assert.Equal(dice, Dice(info.Primary));
        Assert.Equal(flat, info.Primary.FlatModifier);
        Assert.Equal(DamageBonusType.None, info.Primary.DamageBonus);
        Assert.True(info.Primary.IsParsed);
        Assert.Equal(raw, info.RawText);
        Assert.Equal(raw, info.Primary.RawText);
    }

    [Theory]
    [InlineData("1d4+БкУ", "1d4", DamageBonusType.Full)]
    [InlineData("1d4 + БкУ", "1d4", DamageBonusType.Full)]
    [InlineData("1d4+Бку", "1d4", DamageBonusType.Full)]
    [InlineData("1d4+БКУ", "1d4", DamageBonusType.Full)]
    [InlineData("1d6+БП", "1d6", DamageBonusType.Full)]
    [InlineData("1d3+Б.К.У.", "1d3", DamageBonusType.Full)]
    [InlineData("1d8+1d4+БкУ", "1d8,1d4", DamageBonusType.Full)]
    [InlineData("1d4 + 1/2 БкУ", "1d4", DamageBonusType.Half)]
    [InlineData("1d3+1/2 БкУ", "1d3", DamageBonusType.Half)]
    [InlineData("1d4+½ БкУ", "1d4", DamageBonusType.Half)]
    [InlineData("БкУ", "", DamageBonusType.Full)]
    public void Parse_DamageBonus_FullOrHalf(string raw, string dice, DamageBonusType bonus)
    {
        var info = DamageFormulaParser.Parse(raw);

        Assert.True(info.IsParsed);
        Assert.Equal(dice, Dice(info.Primary));
        Assert.Equal(bonus, info.Primary!.DamageBonus);
        Assert.Equal(0, info.Primary.FlatModifier);
    }

    [Fact]
    public void Parse_DamageBonusWordy_LeavesTailButParses()
    {
        var info = DamageFormulaParser.Parse("1d6 + бонус к урону");

        Assert.Equal(DamageBonusType.Full, info.Primary!.DamageBonus);
        Assert.Equal("1d6", Dice(info.Primary));
    }

    [Theory]
    [InlineData("2d6+горение", "2d6", "горение")]
    [InlineData("1d3 + шок", "1d3", "шок")]
    [InlineData("1d3+Шок", "1d3", "шок")]
    public void Parse_Effects(string raw, string dice, string effect)
    {
        var info = DamageFormulaParser.Parse(raw);

        Assert.True(info.IsParsed);
        Assert.Equal(dice, Dice(info.Primary));
        Assert.Equal([effect], info.Primary!.Effects);
    }

    [Fact]
    public void Parse_Shotgun_DamageByRangeBands()
    {
        var info = DamageFormulaParser.Parse("4d6/2d6/1d6");

        Assert.True(info.IsParsed);
        Assert.Null(info.Primary);
        Assert.Equal(["Близкая", "Средняя", "Дальняя"], info.RangeDamages!.Select(r => r.RangeLabel));
        Assert.Equal(["4d6", "2d6", "1d6"], info.RangeDamages!.Select(r => Dice(r.Damage)));
        Assert.Equal("4d6", Dice(info.GetDefaultDamage()));
    }

    [Fact]
    public void Parse_ShotgunWithFlats_EachBandParsed()
    {
        var info = DamageFormulaParser.Parse("2d6 + 2 / 1d6+ 1 / 1d4");

        Assert.Equal([2, 1, 0], info.RangeDamages!.Select(r => r.Damage.FlatModifier));
        Assert.Equal(["2d6 + 2", "1d6+ 1", "1d4"], info.RangeDamages!.Select(r => r.Damage.RawText));
    }

    [Fact]
    public void Parse_FourBands_FourthLabelled()
    {
        var info = DamageFormulaParser.Parse("4d6/3d6/2d6/1d6/1d3");

        Assert.Equal(["Близкая", "Средняя", "Дальняя", "Очень дальняя", "Дальность 5"],
            info.RangeDamages!.Select(r => r.RangeLabel));
    }

    [Theory]
    [InlineData("4d10 / 3 метра", "4d10", 3)]
    [InlineData("2d10/5 метров", "2d10", 5)]
    public void Parse_Explosive_BlastRadius(string raw, string dice, int radius)
    {
        var info = DamageFormulaParser.Parse(raw);

        Assert.True(info.IsParsed);
        Assert.Equal(dice, Dice(info.Primary));
        Assert.Equal(radius, info.BlastRadiusMeters);
        Assert.Null(info.RangeDamages);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Parse_Empty_ParsedWithNothing(string raw)
    {
        var info = DamageFormulaParser.Parse(raw);

        Assert.True(info.IsParsed);
        Assert.Null(info.GetDefaultDamage());
    }

    [Theory]
    [InlineData("Варьирует")]
    [InlineData("abc")]
    [InlineData("1d")]
    [InlineData("d")]
    public void Parse_Garbage_NotParsed(string raw)
    {
        var info = DamageFormulaParser.Parse(raw);

        Assert.False(info.IsParsed);
        Assert.Null(info.Primary);
        Assert.Equal(raw, info.RawText);
    }

    /// <summary>
    ///     F-P13: «Шок» без костей разбирается как формула с одним эффектом. Комментарий
    ///     в <c>Parse</c> (шаг 4) и описание <c>WeaponStatsParser</c> обещают, что такая строка
    ///     остаётся неразобранной.
    /// </summary>
    [Fact]
    [Trait("finding", "F-P13")]
    public void Parse_EffectOnly_IsParsedDespiteComment()
    {
        var info = DamageFormulaParser.Parse("Шок");

        Assert.True(info.IsParsed);
        Assert.Empty(info.Primary!.Dice);
        Assert.Equal(["шок"], info.Primary.Effects);
    }

    /// <summary>
    ///     F-P12: этот разборщик и <c>CombatService.RollDiceFormula</c> понимают формулы по-разному:
    ///     голое число и пробелы внутри «1 d 6» бросок понимает, разборщик — нет; русскую «д» —
    ///     наоборот.
    /// </summary>
    [Theory]
    [Trait("finding", "F-P12")]
    // face — грань, которую выпадет d6 у броска (0 — бросок костей не бросает вовсе)
    [InlineData("5", false, 0, 5)]
    [InlineData("1 d 6", false, 4, 4)]
    [InlineData("1д6", true, 0, 0)]
    public void Parse_DisagreesWithRollDiceFormula(string raw, bool parsed, int face, int rolled)
    {
        Assert.Equal(parsed, DamageFormulaParser.Parse(raw).IsParsed);

        using var dice = face > 0 ? ScriptedRandom.Use(face) : ScriptedRandom.Use();
        Assert.Equal(rolled, CombatService.RollDiceFormula(raw));
        Assert.Equal(0, dice.Remaining);
    }

    [Fact]
    [Trait("finding", "F-P12")]
    public void Parse_CyrillicDice_RollsThroughExpression()
    {
        var expression = DamageFormulaParser.Parse("1д6+1").Primary!;

        using var dice = ScriptedRandom.Use(4);
        Assert.Equal(5, CombatService.RollDamageExpression(expression));
        Assert.Equal(7, CombatService.MaximizeDamageExpression(expression));
    }

    [Theory]
    [InlineData("2d6+4", "2d6 +4")]
    [InlineData("1d4 + 1/2 БкУ", "1d4 + ½ Б.К.У.")]
    [InlineData("1d4+БкУ", "1d4 + Б.К.У.")]
    [InlineData("1d6-1d4-1", "1d6 -1d4 -1")]
    [InlineData("1d3 + шок", "1d3 + шок")]
    [InlineData("БкУ", "Б.К.У.")]
    public void DamageExpression_ToString(string raw, string expected) =>
        Assert.Equal(expected, DamageFormulaParser.Parse(raw).Primary!.ToString());

    [Theory]
    [InlineData(2, 6, false, 12)]
    [InlineData(1, 4, true, -4)]
    public void DiceTerm_MaxValue(int count, int sides, bool negative, int expected) =>
        Assert.Equal(expected, new DiceTerm(count, sides, negative).MaxValue);
}
