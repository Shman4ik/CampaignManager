using CampaignManager.Core.Dice;
using CampaignManager.Core.Encounters;
using CampaignManager.Core.Tests.Infrastructure;
using static CampaignManager.Core.Tests.Encounters.Combat.Fighters;

namespace CampaignManager.Core.Tests.Encounters.Combat;

/// <summary>Стрельба (перенесено из T0.2 <c>RangedAttackTests</c>): дальность, осечка, патроны, очередь, шальная пуля, прицел.</summary>
public sealed class RangedAttackTests
{
    private readonly EncounterParticipant _shooter = Make("Стрелок");
    private readonly EncounterParticipant _target = Make("Культист", side: EncounterSide.Enemies);
    private readonly EncounterState _state;

    public RangedAttackTests()
    {
        _shooter.Profile.Attacks.Add(Revolver());
        _state = Battle(_shooter, _target);
    }

    private static CombatAttack Revolver(int? malfunction = 100) => new()
    {
        Key = "colt", Name = "Револьвер .38", Damage = "1D10", Skill = 60, Kind = CombatAttackKind.Ranged, Impaling = true,
        AmmoCapacity = 6, Malfunction = malfunction,
    };

    private RangedAttackSetup Setup(int roll, RangeBand range = RangeBand.Base) => new()
    {
        AttackerId = _shooter.Id,
        DefenderId = _target.Id,
        AttackKey = "colt",
        Range = range,
        AttackRoll = Rolled(roll),
        DamageRoll = 3,
        ExtraImpaleRoll = 2,
        ConRoll = Rolled(10),
    };

    private AttackOutcome Resolve(RangedAttackSetup setup, ScriptedDice? dice = null) => CombatRules.Ranged(_state, setup, dice ?? ScriptedDice.Of());

    private void Malfunction(int? threshold) => _shooter.Profile.Attacks[0] = Revolver(threshold);

    [Fact]
    [Trait("page", "110")]
    public void BaseRange_RegularSuccessHits_NoDamageBonus()
    {
        _shooter.Stats.DamageBonus = "+1D4";

        var outcome = Resolve(Setup(40));

        Assert.True(outcome.Hit);
        Assert.True(outcome.Damage!.Impaling is false); // обычный успех — без проникающей
        Assert.Equal(SuccessLevel.Regular, outcome.Required);
        Assert.Equal(3, outcome.Damage.Rolled);
        Assert.Equal(0, outcome.Damage.Bonus); // стрельба — без БкУ
        Assert.Equal(3, outcome.Damage.Total);
    }

    [Theory]
    [Trait("page", "110")]
    [InlineData(RangeBand.Long, 30, true)]
    [InlineData(RangeBand.Long, 31, false)]
    [InlineData(RangeBand.Extreme, 12, true)]
    [InlineData(RangeBand.Extreme, 13, false)]
    public void RangeSetsRequiredLevel(RangeBand range, int roll, bool hits)
    {
        var outcome = Resolve(Setup(roll, range));

        Assert.Equal(hits, outcome.Hit);
        Assert.Equal(Check.Required(AttackModifiers.RequiredDifficulty(range)), outcome.Required);
    }

    [Fact]
    [Trait("page", "110")]
    public void LongRangeMiss_SummaryNamesDifficulty() =>
        Assert.Equal("Стрелок промахивается. Цель: Культист (40, нужен трудный успех).", Resolve(Setup(40, RangeBand.Long)).Summary);

    [Theory]
    [Trait("page", "88")]
    [InlineData(RangeBand.Base, 97, SuccessLevel.Failure)]
    [InlineData(RangeBand.Long, 97, SuccessLevel.Fumble)] // нужно 30 — крах уже на 96
    [InlineData(RangeBand.Long, 95, SuccessLevel.Failure)]
    public void FumbleDependsOnRange(RangeBand range, int roll, SuccessLevel expected)
    {
        Malfunction(null);

        var outcome = Resolve(Setup(roll, range));

        Assert.Equal(expected, outcome.Level);
        Assert.False(outcome.Hit);
    }

    [Theory]
    [Trait("page", "110")]
    [InlineData(1, true)]
    [InlineData(5, false)] // чрезвычайный успех, но не 01
    public void ExtremeRange_ImpalesOnlyOnCritical(int roll, bool impales)
    {
        var outcome = Resolve(Setup(roll, RangeBand.Extreme));

        Assert.True(outcome.Hit);
        Assert.Equal(impales, outcome.Damage!.Impaling);
        Assert.Equal(impales ? 2 : 0, outcome.Damage.Extra);
    }

    [Fact]
    [Trait("page", "101")]
    public void ExtremeSuccess_MaxDamagePlusImpale()
    {
        var outcome = Resolve(Setup(10));

        Assert.True(outcome.Damage!.Extreme);
        Assert.False(outcome.Damage.Critical);
        Assert.Equal(10, outcome.Damage.Rolled); // 1D10 на максимум, вписанный урон не нужен
        Assert.Equal(2, outcome.Damage.Extra);
        Assert.Equal(12, outcome.Damage.Raw);
    }

    [Theory]
    [Trait("page", "113")]
    [InlineData(100, 100, true)]
    [InlineData(100, 99, false)]
    [InlineData(98, 98, true)]
    [InlineData(98, 97, false)]
    [InlineData(null, 100, false)] // осечки нет
    public void Malfunction_RollAtOrAboveThreshold(int? threshold, int roll, bool jams)
    {
        Malfunction(threshold);

        var outcome = Resolve(Setup(roll), jams ? ScriptedDice.Of(4) : ScriptedDice.Of());
        Apply(_state, outcome);

        Assert.Equal(jams, outcome.Malfunction);
        if (!jams)
            return;
        Assert.False(outcome.Hit);
        Assert.Equal(4, outcome.JamRounds);
        Assert.Equal("colt", _shooter.Combat.JammedAttack);
        Assert.Equal(4, _shooter.Combat.JamRoundsLeft);
        Assert.NotNull(CombatRules.WeaponBlockReason(_shooter, _shooter.Profile.Attacks[0]));
    }

    [Fact]
    [Trait("page", "113")]
    public void Malfunction_JamRoundsEntered() =>
        Assert.Equal(2, Resolve(Setup(100) with { JamRounds = 2 }).JamRounds);

    /// <summary>F-C02 исправлено: осечка, патрон и счётчик очереди меняются только при «Применить».</summary>
    [Fact]
    [Trait("page", "113")]
    [Trait("finding", "F-C02")]
    public void JamAndAmmo_OnlyOnApply()
    {
        var outcome = Resolve(Setup(100) with { JamRounds = 4 });
        EncounterEngine.Propose(_state, outcome.Resolution);
        EncounterEngine.Cancel(_state);

        Assert.Null(_shooter.Combat.JammedAttack);
        Assert.Equal(6, CombatRules.AmmoLeft(_shooter, _shooter.Profile.Attacks[0]));
        Assert.Equal(0, _shooter.Combat.AttacksIn(_state.Round));

        Apply(_state, outcome);
        Assert.Equal("colt", _shooter.Combat.JammedAttack);
        Assert.Equal(5, CombatRules.AmmoLeft(_shooter, _shooter.Profile.Attacks[0]));
        Assert.Equal(1, _shooter.Combat.AttacksIn(_state.Round));
    }

    [Fact]
    [Trait("page", "114")]
    public void Volley_CountsCheckOnApply_NextCheckGetsPenalty()
    {
        var first = Resolve(Setup(40) with { FiringMode = FiringMode.Volley, ShotsFired = 4 });
        Apply(_state, first);

        Assert.Equal(1, _shooter.Combat.AutofireIn(_state.Round));
        Assert.Equal(2, CombatRules.AmmoLeft(_shooter, _shooter.Profile.Attacks[0]));

        var second = Resolve(Setup(40) with { FiringMode = FiringMode.Volley, ShotsFired = 2 });
        Assert.Contains("−1 проверка №2 при автоматической стрельбе", second.Modifiers.Reasons);
    }

    [Fact]
    [Trait("page", "111")]
    public void SpendsAmmo_ShotsFiredAtLeastOne()
    {
        Apply(_state, Resolve(Setup(40) with { ShotsFired = 0 }));

        Assert.Equal(5, CombatRules.AmmoLeft(_shooter, _shooter.Profile.Attacks[0]));
    }

    [Fact]
    [Trait("page", "111")]
    public void EmptyMagazine_Blocked_ReloadFills()
    {
        _shooter.Combat.Ammo["colt"] = 0;

        var blocked = Resolve(Setup(40));
        Assert.NotNull(blocked.Blocked);
        Assert.Empty(blocked.Resolution.Effects);

        Apply(_state, CombatRules.Reload(_state, _shooter.Id, "colt"));
        Assert.Equal(6, CombatRules.AmmoLeft(_shooter, _shooter.Profile.Attacks[0]));
    }

    [Theory]
    [Trait("page", "114")]
    [InlineData(5, false)]
    [InlineData(6, true)]
    public void AutofireBeyondCritical_Impossible(int checkIndex, bool impossible)
    {
        Malfunction(null);

        var outcome = Resolve(Setup(1) with { FiringMode = FiringMode.Volley, AutofireCheckIndex = checkIndex });

        Assert.Equal(impossible, outcome.Impossible);
        // Даже 01 не попадает, когда сложность выше критической
        Assert.Equal(!impossible, outcome.Hit);
        Assert.Equal(SuccessLevel.Critical, outcome.Required);
    }

    [Theory]
    [Trait("page", "114")]
    [InlineData(0, SuccessLevel.Regular, false, 0)]
    [InlineData(1, SuccessLevel.Regular, false, 1)]
    [InlineData(2, SuccessLevel.Regular, false, 2)]
    [InlineData(3, SuccessLevel.Hard, false, 2)]
    [InlineData(4, SuccessLevel.Extreme, false, 2)]
    [InlineData(5, SuccessLevel.Critical, false, 2)]
    [InlineData(6, SuccessLevel.Critical, true, 2)]
    public void EscalateAutofire_FromRegular(int index, SuccessLevel required, bool impossible, int penalty) =>
        Assert.Equal((required, impossible, penalty), CombatRules.EscalateAutofire(Difficulty.Regular, index));

    [Theory]
    [Trait("page", "112")]
    [InlineData(30, 3)]
    [InlineData(55, 5)]
    [InlineData(10, 3)]
    public void VolleySize_SkillTenthAtLeastThree(int skill, int size) => Assert.Equal(size, CombatRules.VolleySize(skill));

    [Fact]
    [Trait("page", "112")]
    public void FumbleIntoMelee_HitsUnluckiestAlly()
    {
        var lucky = Make("Везунчик");
        lucky.Luck = 60;
        var unlucky = Make("Неудачник");
        unlucky.Luck = 20;
        var dead = Make("Покойник");
        dead.Luck = 1;
        dead.Dead = true;
        var enemy = Make("Глубоководный", side: EncounterSide.Enemies);
        enemy.Luck = 0;
        _shooter.Luck = 5;
        foreach (var p in new[] { lucky, unlucky, dead, enemy })
            EncounterEngine.Add(_state, p, Now);
        Malfunction(null);

        var outcome = Resolve(Setup(100) with { FiringIntoMelee = true });

        Assert.Equal(SuccessLevel.Fumble, outcome.Level);
        Assert.False(outcome.Hit);
        Assert.True(outcome.HitAllyOnFumble);
        Assert.Equal(unlucky.Id, outcome.HitAllyId);
        Assert.Contains("Неудачник", outcome.Summary);
    }

    [Fact]
    [Trait("page", "112")]
    public void FumbleIntoMelee_NoAlly_AskKeeper()
    {
        Malfunction(null);

        var outcome = Resolve(Setup(100) with { FiringIntoMelee = true });

        Assert.True(outcome.HitAllyOnFumble);
        Assert.Null(outcome.HitAllyId);
        Assert.Contains("выберите пострадавшего", outcome.Summary);
    }

    [Fact]
    [Trait("page", "112")]
    public void UnluckiestAlly_SameSideWithLuckOnly()
    {
        var neutral = Make("Прохожий", side: EncounterSide.Neutral);
        neutral.Luck = 1;
        var ally = Make("Союзник");
        ally.Luck = 40;
        var monster = Make("Тварь");
        monster.Luck = null; // Удачи у чудовищ нет
        foreach (var p in new[] { neutral, ally, monster })
            EncounterEngine.Add(_state, p, Now);

        Assert.Same(ally, CombatRules.UnluckiestAlly(_state, _shooter, _target));
        Assert.Null(CombatRules.UnluckiestAlly(_state, _target, _shooter));
    }

    [Fact]
    [Trait("page", "111")]
    public void Hit_ConsumesAim()
    {
        _shooter.Combat.Aiming = true;

        var outcome = Resolve(Setup(40));
        Apply(_state, outcome);

        Assert.True(outcome.Hit);
        Assert.Contains("+1 прицеливание", outcome.Modifiers.Reasons);
        Assert.False(_shooter.Combat.Aiming);
    }

    /// <summary>F-C07 исправлено: прицел даёт бонусную кость одному выстрелу и тратится им, попал он или нет (стр. 111).</summary>
    [Fact]
    [Trait("page", "111")]
    [Trait("finding", "F-C07")]
    public void Miss_AlsoConsumesAim()
    {
        _shooter.Combat.Aiming = true;

        var outcome = Resolve(Setup(70));
        Apply(_state, outcome);

        Assert.False(outcome.Hit);
        Assert.False(_shooter.Combat.Aiming);
    }

    [Fact]
    [Trait("page", "105")]
    public void Surprise_AutoHitInRanged_BecomesBonusDie()
    {
        var outcome = Resolve(Setup(40) with { Surprise = SurpriseMode.AutoHit });

        Assert.Contains("+1 цель застигнута врасплох", outcome.Modifiers.Reasons);
    }

    [Fact]
    [Trait("page", "111")]
    public void TargetCoverFromState_PenaltyOnce()
    {
        Apply(_state, CombatRules.TakeCover(_state, _target.Id, Rolled(10), ScriptedDice.Of()));

        var outcome = Resolve(Setup(40) with { TargetTakingCover = true });

        Assert.Single(outcome.Modifiers.Reasons, r => r.Contains("укрылась", StringComparison.Ordinal));
    }
}
