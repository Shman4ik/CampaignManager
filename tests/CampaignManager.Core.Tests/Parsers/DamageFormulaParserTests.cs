using CampaignManager.Core.Catalogs;
using CampaignManager.Core.Dice;
using CampaignManager.Core.Tests.Infrastructure;

namespace CampaignManager.Core.Tests.Parsers;

/// <summary>
/// Колонка «Урон» таблицы XVII: формулы костей, бонус к урону, дробовики, взрывчатка. Перенесено из T0.2
/// без правки ожиданий, кроме находки F-P12: разбор и бросок теперь понимают одно и то же.
/// </summary>
[Trait("page", "399-402")]
public sealed class DamageFormulaParserTests
{
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
    [InlineData(null)]
    public void Parse_Empty_ParsedWithNothing(string? raw)
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
    /// F-P13 исправлена (T2.1): в v1 «Шок» без костей разбирался как формула с одним эффектом, хотя
    /// комментарии обещали текст. Теперь эффект без костей — неразобранная строка; при костях эффект
    /// по-прежнему разбирается.
    /// </summary>
    [Fact]
    [Trait("finding", "F-P13")]
    public void Parse_EffectOnly_StaysText()
    {
        var info = DamageFormulaParser.Parse("Шок");

        Assert.False(info.IsParsed);
        Assert.Null(info.Primary);
        Assert.Equal("Шок", info.RawText);

        var withDice = DamageFormulaParser.Parse("2d6+горение");
        Assert.True(withDice.IsParsed);
        Assert.Equal(["горение"], withDice.Primary!.Effects);
    }

    /// <summary>
    /// F-P12 исправлена: в v1 голое «5» и «1 d 6» бросок понимал, а разборщик нет; русскую «д» —
    /// наоборот. Теперь разбор и бросок дают одно и то же.
    /// </summary>
    [Theory]
    [Trait("finding", "F-P12")]
    [InlineData("5", new int[0], 5)]
    [InlineData("1 d 6", new[] { 4 }, 4)]
    [InlineData("1д6", new[] { 4 }, 4)]
    public void Parse_AgreesWithDiceFormula(string raw, int[] faces, int rolled)
    {
        var expression = DamageFormulaParser.Parse(raw).Primary;

        Assert.NotNull(expression);
        Assert.Equal(rolled, expression.Roll(ScriptedDice.Of(faces)));
        Assert.Equal(rolled, DiceFormula.Roll(raw, ScriptedDice.Of(faces)));
    }

    [Fact]
    [Trait("finding", "F-P12")]
    public void Parse_CyrillicDice_RollsThroughExpression()
    {
        var expression = DamageFormulaParser.Parse("1д6+1").Primary!;

        Assert.Equal(5, expression.Roll(ScriptedDice.Of(4)));
        Assert.Equal(7, expression.Max);
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
    public void DiceTerm_Max(int count, int sides, bool negative, int expected) =>
        Assert.Equal(expected, new DiceTerm(count, sides, negative).Max);
}
