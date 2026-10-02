using CampaignManager.Core.Characters;
using CampaignManager.Core.Dice;
using static CampaignManager.Core.Tests.Sheet.Sheets;

namespace CampaignManager.Core.Tests.Sheet;

/// <summary>
/// Один конвейер ран (T2.6b) — схема получения урона ширмы Хранителя (стр. 119) и решение владельца по F-S02: тест на
/// каждую ветку. Пороги серьёзной раны перенесены из T0.2 без правки.
/// </summary>
public sealed class WoundRulesTests
{
    private static WoundStatus Healthy(int hp = 13) => new(hp);

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

    // ── Путь 1: урон одной атаки ≥ максимума ПЗ — мгновенная смерть ──

    /// <summary>
    /// Граница «равно максимуму»: решение владельца 2026-10-02 по официальному тексту 7e — «greater than or equal»; русский
    /// перевод в списке стр. 118 пишет «больше» — ошибка перевода (F-S02). Максимум − 1 — ещё жив.
    /// </summary>
    [Theory]
    [Trait("page", "118")]
    [Trait("finding", "F-S02")]
    [InlineData(13, true)] // ровно максимум — смерть
    [InlineData(20, true)]
    [InlineData(12, false)] // на единицу меньше — серьёзная рана и при смерти, но жив
    public void TakeDamage_AtLeastMaxHp_InstantDeath(int damage, bool dies)
    {
        var outcome = WoundRules.TakeDamage(Healthy(13), 13, damage, conPassed: true);

        Assert.Equal(dies, outcome.InstantDeath);
        Assert.Equal(dies, outcome.After.Dead);
        Assert.Equal(dies ? 0 : 1, outcome.After.HitPoints);
        Assert.Equal(!dies, outcome.After.MajorWound); // 12 из 13 — серьёзная рана, жив
    }

    [Fact]
    [Trait("page", "118")]
    [Trait("finding", "F-S02")]
    public void TakeDamage_InstantDeath_ComparesWithMaxNotCurrent()
    {
        // Осталось 3 из 13 — но 12 урона меньше максимума: не мгновенная смерть, а при смерти.
        var outcome = WoundRules.TakeDamage(Healthy(3), 13, 12, conPassed: true);

        Assert.False(outcome.InstantDeath);
        Assert.True(outcome.After.Dying);
    }

    [Fact]
    [Trait("page", "117")]
    public void TakeDamage_NonPositiveOrDead_ChangesNothing()
    {
        Assert.Equal(Healthy(), WoundRules.TakeDamage(Healthy(), 13, 0).After);
        var dead = new WoundStatus(0, Dead: true);
        Assert.Equal(dead, WoundRules.TakeDamage(dead, 13, 5).After);
    }

    // ── Серьёзная рана: падает, ВЫН, при провале без сознания ──

    [Theory]
    [Trait("page", "117")]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void TakeDamage_MajorWound_ConCheckDecidesConsciousness(bool conPassed, bool unconscious)
    {
        var outcome = WoundRules.TakeDamage(Healthy(13), 13, 7, conPassed);

        Assert.True(outcome.MajorWound);
        Assert.True(outcome.FallsProne);
        Assert.True(outcome.After.MajorWound);
        Assert.Equal(6, outcome.After.HitPoints);
        Assert.Equal(unconscious, outcome.After.Unconscious);
        Assert.False(outcome.After.Dying);
    }

    [Fact]
    [Trait("page", "117")]
    public void TakeDamage_MajorWound_NoConCheck_StaysConscious_AsksForCheck()
    {
        var outcome = WoundRules.TakeDamage(Healthy(13), 13, 7);

        Assert.True(outcome.ConCheckMissing);
        Assert.False(outcome.After.Unconscious);
        Assert.Contains("нужна проверка ВЫН", outcome.Note());
    }

    // ── Путь 2: ПЗ упали до 0 ──

    [Fact]
    [Trait("page", "118")]
    public void TakeDamage_ToZeroWithoutMajorWound_Unconscious_NotDying()
    {
        var outcome = WoundRules.TakeDamage(Healthy(3), 13, 5);

        Assert.Equal(0, outcome.After.HitPoints);
        Assert.True(outcome.After.Unconscious);
        Assert.False(outcome.After.Dying);
        Assert.False(outcome.After.Dead);
    }

    [Theory]
    [Trait("page", "118")]
    [InlineData(true, 3)] // рана была раньше
    [InlineData(false, 7)] // рана этим же ударом
    public void TakeDamage_ToZeroWithMajorWound_Dying_AndUnconscious(bool hadMajorWound, int damage)
    {
        var outcome = WoundRules.TakeDamage(new WoundStatus(3, MajorWound: hadMajorWound), 13, damage, conPassed: true);

        Assert.Equal(0, outcome.After.HitPoints);
        Assert.True(outcome.After.Dying);
        Assert.True(outcome.After.Unconscious);
        Assert.False(outcome.After.Dead);
    }

    // ── Путь 3: при смерти — первая помощь в раунде или ВЫН в конце раунда ──

    [Fact]
    [Trait("page", "118")]
    public void FirstAid_Dying_TemporaryStabilization_OneHp_DyingMarkStays()
    {
        var dying = new WoundStatus(0, MajorWound: true, Unconscious: true, Dying: true);

        var outcome = WoundRules.FirstAid(dying, 13);

        Assert.Null(outcome.Refusal);
        Assert.True(outcome.After.Stabilized);
        Assert.True(outcome.After.Dying);
        Assert.Equal(1, outcome.After.HitPoints);
    }

    [Theory]
    [Trait("page", "118")]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void DyingCheck_EndOfRound_FailureIsDeath(bool conPassed, bool dead)
    {
        var dying = new WoundStatus(0, MajorWound: true, Unconscious: true, Dying: true);

        var after = WoundRules.DyingCheck(dying, conPassed).After;

        Assert.Equal(dead, after.Dead);
        Assert.Equal(!dead, after.Dying);
    }

    // ── Путь 4: стабилизирован — Медицина в течение часа или ВЫН в конце часа ──

    [Fact]
    [Trait("page", "118")]
    public void Medicine_StabilizedDying_RemovesDying_AddsRoll()
    {
        var stabilized = new WoundStatus(1, MajorWound: true, Unconscious: true, Dying: true, Stabilized: true);

        var outcome = WoundRules.Medicine(stabilized, 13, 2);

        Assert.False(outcome.After.Dying);
        Assert.False(outcome.After.Stabilized);
        Assert.False(outcome.After.Unconscious);
        Assert.True(outcome.After.MajorWound); // дальше — недельное лечение раны
        Assert.Equal(3, outcome.After.HitPoints);
    }

    [Fact]
    [Trait("page", "118")]
    public void Medicine_DyingWithoutFirstAid_Refused()
    {
        var dying = new WoundStatus(0, MajorWound: true, Unconscious: true, Dying: true);

        var outcome = WoundRules.Medicine(dying, 13, 3);

        Assert.NotNull(outcome.Refusal);
        Assert.Equal(dying, outcome.After);
    }

    [Theory]
    [Trait("page", "119")]
    [InlineData(true)]
    [InlineData(false)]
    public void DyingCheck_Stabilized_EndOfHour_FailureBackToDying(bool conPassed)
    {
        var stabilized = new WoundStatus(1, MajorWound: true, Unconscious: true, Dying: true, Stabilized: true);

        var after = WoundRules.DyingCheck(stabilized, conPassed).After;

        Assert.False(after.Dead); // провал часовой проверки — не смерть, а снова при смерти
        Assert.True(after.Dying);
        Assert.Equal(conPassed, after.Stabilized);
        Assert.Equal(conPassed ? 1 : 0, after.HitPoints);
    }

    // ── Без сознания без раны: первая помощь +1, Медицина +1d3, 1 ПЗ в день ──

    [Fact]
    [Trait("page", "118")]
    public void FirstAid_NotDying_PlusOne_AndWakesUp()
    {
        var outcome = WoundRules.FirstAid(new WoundStatus(0, Unconscious: true), 13);

        Assert.Equal(1, outcome.After.HitPoints);
        Assert.False(outcome.After.Unconscious);
    }

    [Fact]
    [Trait("page", "118")]
    public void Medicine_NotDying_PlusRoll_CappedAtMax()
    {
        Assert.Equal(13, WoundRules.Medicine(new WoundStatus(12), 13, 3).After.HitPoints);
        Assert.Equal(7, WoundRules.Medicine(new WoundStatus(4, Unconscious: true), 13, 3).After.HitPoints);
    }

    [Theory]
    [Trait("page", "119")]
    [InlineData(false, 3, 7)]
    [InlineData(true, 3, 4)] // при серьёзной ране — только недельная проверка
    public void NaturalRecovery_OnePerDay_OnlyWithoutMajorWound(bool majorWound, int days, int expected) =>
        Assert.Equal(expected, WoundRules.NaturalRecovery(new WoundStatus(4, MajorWound: majorWound), 13, days).After.HitPoints);

    // ── Лечение серьёзной раны: ВЫН раз в неделю ──

    [Theory]
    [Trait("page", "119")]
    [InlineData(SuccessLevel.Fumble, 0, 4, true)]
    [InlineData(SuccessLevel.Failure, 0, 4, true)]
    [InlineData(SuccessLevel.Regular, 2, 6, true)]
    [InlineData(SuccessLevel.Hard, 3, 7, true)]
    [InlineData(SuccessLevel.Extreme, 5, 9, false)] // 2d3 и отметка снята
    [InlineData(SuccessLevel.Critical, 4, 8, false)]
    public void WeeklyRecovery_ByLevel(SuccessLevel level, int amount, int hp, bool stillMajor)
    {
        var after = WoundRules.WeeklyRecovery(new WoundStatus(4, MajorWound: true), 15, level, amount).After;

        Assert.Equal(hp, after.HitPoints);
        Assert.Equal(stillMajor, after.MajorWound);
    }

    [Theory]
    [Trait("page", "119")]
    [InlineData(SuccessLevel.Failure, 0)]
    [InlineData(SuccessLevel.Regular, 1)]
    [InlineData(SuccessLevel.Hard, 1)]
    [InlineData(SuccessLevel.Extreme, 2)]
    public void RecoveryDiceCount_ByLevel(SuccessLevel level, int dice) => Assert.Equal(dice, WoundRules.RecoveryDiceCount(level));

    [Fact]
    [Trait("page", "119")]
    public void RecoveryDice_CareRestAndPoorConditions() =>
        Assert.Equal((2, 1), WoundRules.RecoveryDice(medicalCare: true, rested: true, poorConditions: true));

    // ── Лист: те же правила ──

    [Fact]
    [Trait("page", "118")]
    [Trait("finding", "F-S02")]
    public void ApplyDamage_Sheet_AtLeastMaxHp_Dead()
    {
        var sheet = Wounded(13, 13);

        var outcome = WoundRules.ApplyDamage(sheet, 13);

        Assert.True(outcome.InstantDeath);
        Assert.True(sheet.Condition.Dead);
        Assert.False(sheet.Condition.Dying);
        Assert.Equal(0, sheet.Current.HitPoints);
    }

    [Fact]
    [Trait("page", "117")]
    public void ApplyDamage_Sheet_MaxFromCharacteristicsWhenNotOverridden()
    {
        var sheet = NewSheet(); // ВЫН 80 + ТЕЛ 60 → 14 ПЗ, серьёзная рана с 7
        sheet.Current.HitPoints = 14;

        Assert.False(WoundRules.ApplyDamage(sheet, 6).MajorWound);
        Assert.True(WoundRules.ApplyDamage(sheet, 7).MajorWound);
    }

    [Fact]
    [Trait("page", "118")]
    public void UpdateConsciousness_HpAboveZero_ClearsStates_KeepsWoundAndDeath()
    {
        var sheet = Wounded(1, 13);
        sheet.Condition.Dying = true;
        sheet.Condition.Unconscious = true;
        sheet.Condition.MajorWound = true;

        WoundRules.UpdateConsciousness(sheet);

        Assert.False(sheet.Condition.Dying);
        Assert.False(sheet.Condition.Unconscious);
        Assert.True(sheet.Condition.MajorWound); // рана остаётся

        sheet.Condition.Dead = true;
        sheet.Current.HitPoints = 5;
        WoundRules.UpdateConsciousness(sheet);
        Assert.True(sheet.Condition.Dead); // смерть правкой ПЗ не снимается
    }

    [Fact]
    [Trait("page", "118")]
    public void UpdateConsciousness_Zero_UnconsciousOrDyingByWound()
    {
        var sheet = Wounded(0, 13);
        WoundRules.UpdateConsciousness(sheet);
        Assert.True(sheet.Condition.Unconscious);
        Assert.False(sheet.Condition.Dying);

        sheet.Condition.MajorWound = true;
        WoundRules.UpdateConsciousness(sheet);
        Assert.True(sheet.Condition.Dying);
    }

    private static CharacterSheet Wounded(int hp, int maxHp)
    {
        var sheet = NewSheet();
        sheet.Overrides.MaxHitPoints = maxHp;
        sheet.Current.HitPoints = hp;
        return sheet;
    }
}
