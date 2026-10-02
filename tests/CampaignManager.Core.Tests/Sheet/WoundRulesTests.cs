using CampaignManager.Core.Characters;
using static CampaignManager.Core.Tests.Sheet.Sheets;

namespace CampaignManager.Core.Tests.Sheet;

/// <summary>Серьёзная рана и состояние при нуле ПЗ. Перенесено из T0.2 без правки ожиданий.</summary>
public sealed class WoundRulesTests
{
    [Theory]
    [Trait("page", "117")]
    [InlineData(13, 7)] // половина 6,5 — вверх
    [InlineData(12, 6)]
    [InlineData(11, 6)]
    [InlineData(10, 5)]
    [InlineData(2, 1)]
    [InlineData(1, 1)]
    [InlineData(0, 1)]
    [InlineData(-5, 1)]
    public void MajorWoundThreshold_HalfOfMaxHp_RoundedUp_AtLeastOne(int maxHp, int expected) =>
        Assert.Equal(expected, WoundRules.MajorWoundThreshold(maxHp));

    [Theory]
    [Trait("page", "117")]
    [InlineData(6, 13, false)]
    [InlineData(7, 13, true)]
    [InlineData(5, 12, false)]
    [InlineData(6, 12, true)]
    public void IsMajorWound_DamageAtLeastThreshold(int damage, int maxHp, bool expected) =>
        Assert.Equal(expected, WoundRules.IsMajorWound(damage, maxHp));

    [Theory]
    [Trait("page", "117")]
    [InlineData(0)]
    [InlineData(-3)]
    public void ApplyDamage_NonPositive_ChangesNothing(int damage)
    {
        var sheet = Wounded(13, 13);

        Assert.False(WoundRules.ApplyDamage(sheet, damage));
        Assert.Equal(13, sheet.Current.HitPoints);
    }

    [Fact]
    [Trait("page", "117")]
    public void ApplyDamage_MajorWound_MarksSeriousInjury_StillConscious()
    {
        var sheet = Wounded(13, 13);

        Assert.True(WoundRules.ApplyDamage(sheet, 7));
        Assert.Equal(6, sheet.Current.HitPoints);
        Assert.True(sheet.Condition.MajorWound);
        Assert.False(sheet.Condition.Dying);
        Assert.False(sheet.Condition.Unconscious);
    }

    [Fact]
    [Trait("page", "118")]
    public void ApplyDamage_MinorWoundToZero_Unconscious()
    {
        var sheet = Wounded(3, 13);

        Assert.False(WoundRules.ApplyDamage(sheet, 5));
        Assert.Equal(0, sheet.Current.HitPoints);
        Assert.True(sheet.Condition.Unconscious);
        Assert.False(sheet.Condition.Dying);
    }

    [Fact]
    [Trait("page", "118")]
    public void ApplyDamage_ToZeroWithEarlierSeriousInjury_Dying()
    {
        var sheet = Wounded(3, 13);
        sheet.Condition.MajorWound = true;

        WoundRules.ApplyDamage(sheet, 3);

        Assert.True(sheet.Condition.Dying);
        Assert.False(sheet.Condition.Unconscious);
    }

    /// <summary>
    /// Урон одной атаки не меньше максимума ПЗ — по бою это мгновенная смерть (стр. 118), а лист ставит
    /// «при смерти». Решается в T2.6 вместе с конвейером ран боя.
    /// </summary>
    [Fact]
    [Trait("page", "118")]
    [Trait("finding", "F-S02")]
    public void ApplyDamage_AtLeastMaxHp_MarksDying_NotDead()
    {
        var sheet = Wounded(13, 13);

        Assert.True(WoundRules.ApplyDamage(sheet, 13));
        Assert.Equal(0, sheet.Current.HitPoints);
        Assert.True(sheet.Condition.Dying);
    }

    [Fact]
    [Trait("page", "118")]
    public void UpdateConsciousness_HpAboveZero_ClearsBothStates()
    {
        var sheet = Wounded(1, 13);
        sheet.Condition.Dying = true;
        sheet.Condition.Unconscious = true;
        sheet.Condition.MajorWound = true;

        WoundRules.UpdateConsciousness(sheet);

        Assert.False(sheet.Condition.Dying);
        Assert.False(sheet.Condition.Unconscious);
        Assert.True(sheet.Condition.MajorWound); // рана остаётся
    }

    [Fact]
    [Trait("page", "117")]
    public void ApplyDamage_MaxFromCharacteristicsWhenNotOverridden()
    {
        var sheet = NewSheet(); // ВЫН 80 + ТЕЛ 60 → 14 ПЗ, серьёзная рана с 7
        sheet.Current.HitPoints = 14;

        Assert.False(WoundRules.ApplyDamage(sheet, 6));
        Assert.True(WoundRules.ApplyDamage(sheet, 7));
    }

    private static CharacterSheet Wounded(int hp, int maxHp)
    {
        var sheet = NewSheet();
        sheet.Overrides.MaxHitPoints = maxHp;
        sheet.Current.HitPoints = hp;
        return sheet;
    }
}
