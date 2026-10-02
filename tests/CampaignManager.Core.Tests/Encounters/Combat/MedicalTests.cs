using CampaignManager.Core.Characters;
using CampaignManager.Core.Dice;
using CampaignManager.Core.Encounters;
using CampaignManager.Core.Tests.Infrastructure;
using static CampaignManager.Core.Tests.Encounters.Combat.Fighters;
using static CampaignManager.Core.Tests.Sheet.Sheets;

namespace CampaignManager.Core.Tests.Encounters.Combat;

/// <summary>
/// Первая помощь, Медицина, проверки ВЫН умирающих, лечение — в Core с эффектами (в v1 жили в razor и правили участников
/// мимо «Применить»). Сценарий — пример с Харви (стр. 117–119).
/// </summary>
public sealed class MedicalTests
{
    private readonly EncounterParticipant _harvey = Investigator("Харви");
    private readonly EncounterParticipant _friend = Investigator("Друг");
    private readonly EncounterParticipant _cultist = Make("Культист", side: EncounterSide.Enemies);
    private readonly EncounterState _state;

    public MedicalTests()
    {
        _harvey.MaxHitPoints = 15;
        _harvey.HitPoints = 15;
        _friend.Profile.FirstAid = 60;
        _friend.Profile.Medicine = 50;
        _state = Battle(_harvey, _friend, _cultist);
    }

    private void Hit(int damage, int conRoll = 10) =>
        Apply(_state, CombatRules.Melee(_state, new MeleeAttackSetup
        {
            AttackerId = _cultist.Id, DefenderId = _harvey.Id, AttackSkill = 50, Surprise = SurpriseMode.BonusDie,
            AttackRoll = Rolled(40), DamageRoll = damage, DamageBonusRoll = 0, ConRoll = Rolled(conRoll),
        }, ScriptedDice.Of()));

    private void NextRound()
    {
        var round = _state.Round;
        while (_state.Round == round)
            EncounterQueue.Next(_state, Now);
    }

    [Fact]
    [Trait("page", "117-119")]
    public void Harvey_DyingStabilizedTreated()
    {
        Hit(3);
        Hit(8); // серьёзная рана (≥ 8 из 15), ВЫН пройдена
        Assert.Equal(4, _harvey.HitPoints);
        Assert.True(_harvey.MajorWound);
        Assert.False(_harvey.Unconscious);

        Hit(5); // до нуля при серьёзной ране — при смерти
        Assert.True(_harvey.Dying);
        Assert.True(_harvey.Unconscious);
        Assert.Empty(CombatRules.DyingChecksDue(_state)); // первая проверка — в конце следующего раунда

        NextRound();
        // Первая помощь проваливается…
        Apply(_state, CombatRules.FirstAid(_state, _friend.Id, _harvey.Id, Rolled(90), ScriptedDice.Of()));
        Assert.False(_harvey.Stabilized);
        // …ВЫН в конце раунда — успех
        Assert.Equal([_harvey], CombatRules.DyingChecksDue(_state));
        Apply(_state, CombatRules.DyingCheck(_state, _harvey.Id, Rolled(30), ScriptedDice.Of()));
        Assert.True(_harvey.Dying);
        Assert.Empty(CombatRules.DyingChecksDue(_state));

        NextRound();
        // Вторая попытка первой помощи — успех: стабилизирован, 1 ПЗ, отметка «при смерти» остаётся
        Apply(_state, CombatRules.FirstAid(_state, _friend.Id, _harvey.Id, Rolled(20), ScriptedDice.Of()));
        Assert.True(_harvey.Stabilized);
        Assert.True(_harvey.Dying);
        Assert.Equal(1, _harvey.HitPoints);
        Assert.Empty(CombatRules.DyingChecksDue(_state)); // стабилизированный — раз в час, не каждый раунд

        // Медицина — «при смерти» снято, +1d3 (вписано 2)
        Apply(_state, CombatRules.Medicine(_state, _friend.Id, _harvey.Id, Rolled(20), notSameDay: false, healRoll: 2, ScriptedDice.Of()));
        Assert.False(_harvey.Dying);
        Assert.False(_harvey.Stabilized);
        Assert.Equal(3, _harvey.HitPoints);
        Assert.True(_harvey.MajorWound);
    }

    [Fact]
    [Trait("page", "118")]
    public void DyingCheck_Failure_Death()
    {
        Hit(8);
        Hit(7);
        NextRound();

        Apply(_state, CombatRules.DyingCheck(_state, _harvey.Id, Rolled(80), ScriptedDice.Of()));

        Assert.True(_harvey.Dead);
        Assert.False(_harvey.Dying);
    }

    [Fact]
    [Trait("page", "119")]
    public void StabilizedHourlyCheck_Failure_BackToDying_NeedsFirstAidAgain()
    {
        Hit(8);
        Hit(7);
        Apply(_state, CombatRules.FirstAid(_state, _friend.Id, _harvey.Id, Rolled(20), ScriptedDice.Of()));
        Assert.True(_harvey.Stabilized);
        Assert.NotNull(CombatRules.FirstAidBlockReason(_harvey));

        Apply(_state, CombatRules.DyingCheck(_state, _harvey.Id, Rolled(90), ScriptedDice.Of()));

        Assert.False(_harvey.Dead);
        Assert.True(_harvey.Dying);
        Assert.False(_harvey.Stabilized);
        Assert.Equal(0, _harvey.HitPoints);
        Assert.Null(CombatRules.FirstAidBlockReason(_harvey)); // снова нужна первая помощь
        NextRound();
        Assert.Equal([_harvey], CombatRules.DyingChecksDue(_state));
    }

    [Fact]
    [Trait("page", "118")]
    public void Medicine_DyingWithoutFirstAid_Blocked()
    {
        Hit(8);
        Hit(7);

        var resolution = CombatRules.Medicine(_state, _friend.Id, _harvey.Id, Rolled(5), false, 3, ScriptedDice.Of());

        Assert.Empty(resolution.Effects);
        Assert.Contains("сначала первая помощь", resolution.Title);
    }

    [Theory]
    [Trait("page", "118")]
    [InlineData(false, 25, true)] // 25 ≤ 50
    [InlineData(true, 25, true)] // трудная: нужно ≤ 25
    [InlineData(true, 26, false)]
    public void Medicine_NotSameDay_Hard(bool notSameDay, int roll, bool heals)
    {
        Hit(4);

        var resolution = CombatRules.Medicine(_state, _friend.Id, _harvey.Id, Rolled(roll), notSameDay, 2, ScriptedDice.Of());

        Assert.Equal(heals, resolution.Effects.Any(e => e.Kind == EncounterEffectKind.Medicine));
    }

    [Fact]
    [Trait("page", "118")]
    public void FirstAid_OncePerWound_UntilNewDamage()
    {
        Hit(4);
        Apply(_state, CombatRules.FirstAid(_state, _friend.Id, _harvey.Id, Rolled(20), ScriptedDice.Of()));
        Assert.Equal(12, _harvey.HitPoints);
        Assert.NotNull(CombatRules.FirstAidBlockReason(_harvey));

        Hit(2);
        Assert.Null(CombatRules.FirstAidBlockReason(_harvey));
    }

    [Fact]
    [Trait("page", "118")]
    public void FirstAid_FailedOnce_NextIsPushed()
    {
        Hit(4);
        Apply(_state, CombatRules.FirstAid(_state, _friend.Id, _harvey.Id, Rolled(90), ScriptedDice.Of()));

        var second = CombatRules.FirstAid(_state, _friend.Id, _harvey.Id, Rolled(20), ScriptedDice.Of());

        Assert.Contains("повторная проверка", second.Lines[0]);
    }

    [Fact]
    [Trait("page", "119")]
    public void WeeklyRecovery_ExtremeRemovesMajorWound_BonusDiceForCare()
    {
        Hit(8);
        var dice = ScriptedDice.Of(5, 0, 9, 9, 3, 2); // единицы 5, три кости десятков 0, 9, 9 (две бонусные) → 05; 2d3 = 3 + 2

        var resolution = CombatRules.WeeklyRecovery(_state, new RecoverySetup
        {
            ParticipantId = _harvey.Id, MedicalCare = true, Rested = true,
        }, dice);
        Apply(_state, resolution);

        Assert.Equal(0, dice.Remaining);
        Assert.False(_harvey.MajorWound);
        Assert.Equal(12, _harvey.HitPoints);
    }

    [Fact]
    [Trait("page", "119")]
    public void NaturalRecovery_WithoutMajorWound()
    {
        Hit(4);

        Apply(_state, CombatRules.NaturalRecovery(_state, _harvey.Id, 3));

        Assert.Equal(14, _harvey.HitPoints);
    }

    /// <summary>Итог в лист — те же правила ран: стабилизация, Медицина, смерть умирающего пишутся в документ листа.</summary>
    [Fact]
    [Trait("page", "118")]
    public void SheetGetsWoundPipeline()
    {
        var sheet = NewSheet();
        sheet.Overrides.MaxHitPoints = 15;
        sheet.Current.HitPoints = 4;
        sheet.Condition.MajorWound = true;

        EncounterSheetEffects.Apply(sheet, Catalog,
        [
            new EncounterEffect { Kind = EncounterEffectKind.Damage, Amount = 5 },
            new EncounterEffect { Kind = EncounterEffectKind.FirstAid, Amount = 1 },
        ]);
        Assert.True(sheet.Condition.Dying);
        Assert.True(sheet.Condition.Stabilized);
        Assert.Equal(1, sheet.Current.HitPoints);

        EncounterSheetEffects.Apply(sheet, Catalog, [new EncounterEffect { Kind = EncounterEffectKind.Medicine, Amount = 2 }]);
        Assert.False(sheet.Condition.Dying);
        Assert.Equal(3, sheet.Current.HitPoints);

        EncounterSheetEffects.Apply(sheet, Catalog, [new EncounterEffect { Kind = EncounterEffectKind.Damage, Amount = 15 }]);
        Assert.True(sheet.Condition.Dead);
    }

    [Fact]
    [Trait("page", "118")]
    public void SheetDyingCheckFailure_Dead()
    {
        var sheet = NewSheet();
        WoundRules.Write(sheet, new WoundStatus(0, MajorWound: true, Unconscious: true, Dying: true));

        EncounterSheetEffects.Apply(sheet, Catalog, [new EncounterEffect { Kind = EncounterEffectKind.DyingCheck, Check = false }]);

        Assert.True(sheet.Condition.Dead);
    }

    [Fact]
    [Trait("page", "119")]
    public void SheetRecovery_UsesLevel()
    {
        var sheet = NewSheet();
        sheet.Current.HitPoints = 5;
        sheet.Condition.MajorWound = true;

        EncounterSheetEffects.Apply(sheet, Catalog,
            [new EncounterEffect { Kind = EncounterEffectKind.Recovery, Amount = 4, Level = SuccessLevel.Extreme }]);

        Assert.Equal(9, sheet.Current.HitPoints);
        Assert.False(sheet.Condition.MajorWound);
    }
}
