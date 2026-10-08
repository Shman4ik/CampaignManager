using CampaignManager.Core.Catalogs;
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
        var first = Resolve(Setup(40) with { FiringMode = FiringMode.Volley, ShotsFired = 4, DamageRolls = [3, 4] });
        Apply(_state, first);

        Assert.Equal(1, _shooter.Combat.AutofireIn(_state.Round));
        Assert.Equal(2, CombatRules.AmmoLeft(_shooter, _shooter.Profile.Attacks[0]));

        var second = Resolve(Setup(40) with { FiringMode = FiringMode.Volley, ShotsFired = 2, DamageRolls = [3] });
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

        var outcome = Resolve(Setup(1) with { FiringMode = FiringMode.Volley, AutofireCheckIndex = checkIndex, DamageRolls = [3] });

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

    // ── Метание: от него уклоняются (стр. 106) ─────────────────────────────

    private static CombatAttack Knife() => new()
    {
        Key = "knife", Name = "Метательный нож", Damage = "1D4+½БкУ", Skill = 60, Kind = CombatAttackKind.Ranged, Thrown = true,
        DamageBonus = CreatureDamageBonusMode.Half, Impaling = true,
    };

    private RangedAttackSetup Throw(int roll, int? dodge, SurpriseMode surprise = SurpriseMode.TargetReady) => new()
    {
        AttackerId = _shooter.Id, DefenderId = _target.Id, AttackKey = "knife", Surprise = surprise,
        AttackRoll = Rolled(roll), DefenseRoll = dodge is { } d ? Rolled(d) : null, DamageRoll = 3, DamageBonusRoll = 0, ExtraImpaleRoll = 2, ConRoll = Rolled(10),
    };

    [Theory]
    [Trait("page", "106")]
    [InlineData(40, 10, false)] // обычный успех против трудного уклонения — промах
    [InlineData(40, 20, false)] // ничья обычных — уклонившемуся
    [InlineData(10, 20, true)]  // чрезвычайный против обычного — попадание
    [InlineData(40, 90, true)]  // уклонение провалено
    public void Thrown_TargetDodges_AsInMelee(int attack, int dodge, bool hits)
    {
        _shooter.Profile.Attacks.Add(Knife());

        var outcome = Resolve(Throw(attack, dodge));

        Assert.Equal(hits, outcome.Hit);
        Assert.NotNull(outcome.DefenseRoll);
        Assert.Contains(outcome.Resolution.Effects, e => e.Kind == EncounterEffectKind.Defense && e.ParticipantId == _target.Id);
        if (!hits)
            Assert.Contains("уклонился", outcome.Summary, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("page", "106")]
    public void Thrown_TargetNotReady_DoesNotDodge()
    {
        _shooter.Profile.Attacks.Add(Knife());

        var outcome = Resolve(Throw(40, dodge: null, SurpriseMode.BonusDie));

        Assert.True(outcome.Hit);
        Assert.Null(outcome.DefenseRoll);
    }

    [Fact]
    [Trait("page", "110")]
    public void Firearm_IsNotDodged()
    {
        var outcome = Resolve(Setup(40) with { DefenseRoll = Rolled(1) });

        Assert.True(outcome.Hit);
        Assert.Null(outcome.DefenseRoll);
    }

    // ── Залп очереди: половина пуль, урон каждой (стр. 115) ──────────────────

    [Fact]
    [Trait("page", "115")]
    public void Volley_Success_HalfTheBulletsHit_ArmorFromEach()
    {
        _target.Stats.Armor = 1;

        var outcome = Resolve(Setup(40) with { FiringMode = FiringMode.Volley, ShotsFired = 5, DamageRolls = [3, 7] });

        Assert.True(outcome.Hit);
        Assert.Equal([2, 6], outcome.Bullets.Select(b => b.Total));
        Assert.Equal(8, outcome.Damage!.Total);
        Assert.Equal(2, outcome.Damage.Armor);
        Assert.Contains(outcome.Resolution.Effects, e => e.Kind == EncounterEffectKind.Damage && e.ParticipantId == _target.Id && e.Amount == 8);
        Assert.StartsWith("Стрелок попадает: 2 из 5.", outcome.Summary, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("page", "115")]
    public void Volley_OneBullet_StillHits()
    {
        var outcome = Resolve(Setup(40) with { FiringMode = FiringMode.Volley, ShotsFired = 1, DamageRolls = [4] });

        Assert.Single(outcome.Bullets);
        Assert.Equal(4, outcome.Damage!.Total);
    }

    /// <summary>Пример с Сесилом (стр. 115): чрезвычайный успех — все пули, половина проникающие (максимум и бросок).</summary>
    [Fact]
    [Trait("page", "115")]
    public void Volley_Extreme_AllHit_FirstHalfImpale()
    {
        var outcome = Resolve(Setup(10) with { FiringMode = FiringMode.Volley, ShotsFired = 4, DamageRolls = [2, 3, 4, 5] });

        Assert.Equal(4, outcome.Bullets.Count);
        Assert.Equal([12, 13, 4, 5], outcome.Bullets.Select(b => b.Total));
        Assert.True(outcome.Bullets[0].Impaling);
        Assert.False(outcome.Bullets[2].Impaling);
        Assert.Equal(34, outcome.Damage!.Total);
    }

    [Fact]
    [Trait("page", "115")]
    public void Volley_ExtremeDifficulty_BestIsPlainSuccess()
    {
        var outcome = Resolve(Setup(5, RangeBand.Extreme) with { FiringMode = FiringMode.Volley, ShotsFired = 4, DamageRolls = [3, 3] });

        Assert.Equal(SuccessLevel.Extreme, outcome.Level);
        Assert.Equal(2, outcome.Bullets.Count);
        Assert.All(outcome.Bullets, b => Assert.False(b.Impaling));
    }

    [Fact]
    [Trait("page", "111")]
    public void PistolBurst_OneBulletPerCheck()
    {
        Apply(_state, Resolve(Setup(40) with { FiringMode = FiringMode.PistolBurst, ShotsFired = 3 }));

        Assert.Equal(5, CombatRules.AmmoLeft(_shooter, _shooter.Profile.Attacks[0]));
    }

    // ── Дробовик: урон по дальности, дробь не проникает (стр. 407) ──────────

    private static CombatAttack Shotgun() => new()
    {
        Key = "shotgun", Name = "Дробовик 12-го калибра", Damage = "4d6/2d6/1d6", Skill = 60, Kind = CombatAttackKind.Ranged,
        AmmoCapacity = 2, Malfunction = 100,
    };

    [Theory]
    [Trait("page", "407")]
    [InlineData(RangeBand.Base, "4d6")]
    [InlineData(RangeBand.Long, "2d6")]
    [InlineData(RangeBand.Extreme, "1d6")]
    public void Shotgun_DamageByRange(RangeBand range, string formula)
    {
        _shooter.Profile.Attacks.Add(Shotgun());

        var outcome = Resolve(Setup(5, range) with { AttackKey = "shotgun" });

        Assert.True(outcome.Hit);
        Assert.Equal(formula, outcome.Damage!.Formula);
    }

    [Fact]
    [Trait("page", "407")]
    public void Shotgun_ExtremeAtCloseRange_MaxWithoutImpale()
    {
        _shooter.Profile.Attacks.Add(Shotgun());

        var outcome = Resolve(Setup(10) with { AttackKey = "shotgun" });

        Assert.Equal(24, outcome.Damage!.Total);
        Assert.Equal(0, outcome.Damage.Extra);
    }
}
