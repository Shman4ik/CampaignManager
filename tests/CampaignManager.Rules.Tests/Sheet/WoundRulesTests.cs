using CampaignManager.Web.Components.Features.Characters.Model;
using CampaignManager.Web.Components.Features.Characters.Services;
using static CampaignManager.Rules.Tests.Sheet.Sheets;

namespace CampaignManager.Rules.Tests.Sheet;

/// <summary>Серьёзная рана и состояние при нуле ПЗ.</summary>
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
        var character = Wounded(13, 13);

        Assert.False(WoundRules.ApplyDamage(character, damage));
        Assert.Equal(13, character.DerivedAttributes.HitPoints.Value);
    }

    [Fact]
    [Trait("page", "117")]
    public void ApplyDamage_MajorWound_MarksSeriousInjury_StillConscious()
    {
        var character = Wounded(13, 13);

        Assert.True(WoundRules.ApplyDamage(character, 7));
        Assert.Equal(6, character.DerivedAttributes.HitPoints.Value);
        Assert.True(character.State.HasSeriousInjury);
        Assert.False(character.State.IsDying);
        Assert.False(character.State.IsUnconscious);
    }

    [Fact]
    [Trait("page", "118")]
    public void ApplyDamage_MinorWoundToZero_Unconscious()
    {
        var character = Wounded(3, 13);

        Assert.False(WoundRules.ApplyDamage(character, 5));
        Assert.Equal(0, character.DerivedAttributes.HitPoints.Value);
        Assert.True(character.State.IsUnconscious);
        Assert.False(character.State.IsDying);
    }

    [Fact]
    [Trait("page", "118")]
    public void ApplyDamage_ToZeroWithEarlierSeriousInjury_Dying()
    {
        var character = Wounded(3, 13);
        character.State.HasSeriousInjury = true;

        WoundRules.ApplyDamage(character, 3);

        Assert.True(character.State.IsDying);
        Assert.False(character.State.IsUnconscious);
    }

    /// <summary>
    ///     Урон одной атаки не меньше максимума ПЗ — по бою это мгновенная смерть
    ///     (<c>CombatService</c>, стр. 118), а лист ставит лишь «при смерти».
    /// </summary>
    [Fact]
    [Trait("page", "118")]
    [Trait("finding", "F-S02")]
    public void ApplyDamage_AtLeastMaxHp_MarksDying_NotDead()
    {
        var character = Wounded(13, 13);

        Assert.True(WoundRules.ApplyDamage(character, 13));
        Assert.Equal(0, character.DerivedAttributes.HitPoints.Value);
        Assert.True(character.State.IsDying);
    }

    [Fact]
    [Trait("page", "118")]
    public void UpdateConsciousness_HpAboveZero_ClearsBothStates()
    {
        var character = Wounded(1, 13);
        character.State.IsDying = true;
        character.State.IsUnconscious = true;
        character.State.HasSeriousInjury = true;

        WoundRules.UpdateConsciousness(character);

        Assert.False(character.State.IsDying);
        Assert.False(character.State.IsUnconscious);
        Assert.True(character.State.HasSeriousInjury); // рана остаётся
    }

    private static Character Wounded(int hp, int maxHp)
    {
        var character = Character();
        character.DerivedAttributes.HitPoints = new AttributeWithMaxValue(hp, maxHp);
        return character;
    }
}
