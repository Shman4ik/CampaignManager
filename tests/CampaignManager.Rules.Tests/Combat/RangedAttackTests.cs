using CampaignManager.Rules.Tests.Infrastructure;
using CampaignManager.Web.Components.Features.Combat.Model;
using CampaignManager.Web.Components.Features.Combat.Services;
using CampaignManager.Web.Components.Features.Weapons.Model;

namespace CampaignManager.Rules.Tests.Combat;

/// <summary>Стрельба: сложность по дальности, осечка, невозможный выстрел, шальная пуля.</summary>
public sealed class RangedAttackTests
{
    private readonly Combatant _shooter = Fighters.Make("Стрелок");
    private readonly Combatant _target = Fighters.Make("Культист", side: CombatSide.Enemy);
    private readonly CombatService _combat;

    public RangedAttackTests() => _combat = Fighters.Battle(_shooter, _target);

    private AttackSetup Setup(int roll, int skill = 60, RangeLevel range = RangeLevel.Base) => new()
    {
        AttackerId = _shooter.Id,
        DefenderId = _target.Id,
        IsMelee = false,
        AttackSkillValue = skill,
        RangeLevel = range,
        ManualAttackerRoll = roll,
        ManualWeaponDamageRoll = 3,
        ManualExtraImpalingRoll = 2
    };

    private static Weapon Revolver(int? malfunction = 100) => new()
    {
        Name = "Револьвер .38",
        Type = WeaponType.Pistols,
        Damage = "1D10",
        MalfunctionThreshold = malfunction,
        AmmoInfo = new WeaponAmmoInfo { Capacity = 6 }
    };

    [Fact]
    [Trait("page", "110")]
    public void ResolveRangedAttack_BaseRange_RegularSuccessHits_NoDamageBonus()
    {
        _shooter.DamageBonus = "+1D4";

        var result = _combat.ResolveRangedAttack(Setup(40));

        Assert.True(result.AttackerWins);
        Assert.True(result.IsImpalingWeapon);
        Assert.Equal(SuccessLevel.RegularSuccess, result.RequiredSuccessLevel);
        Assert.Equal(3, result.DamageRolled);
        Assert.Equal(0, result.BonusDamage); // дальний бой — без БкУ
        Assert.Equal(3, result.TotalDamage);
    }

    [Theory]
    [Trait("page", "110")]
    [InlineData(RangeLevel.Long, 30, true)]
    [InlineData(RangeLevel.Long, 31, false)]
    [InlineData(RangeLevel.Extreme, 12, true)]
    [InlineData(RangeLevel.Extreme, 13, false)]
    public void ResolveRangedAttack_RangeSetsRequiredLevel(RangeLevel range, int roll, bool hits)
    {
        var result = _combat.ResolveRangedAttack(Setup(roll, 60, range));

        Assert.Equal(hits, result.AttackerWins);
        Assert.Equal(CombatService.GetRequiredLevelForRange(range), result.RequiredSuccessLevel);
    }

    [Fact]
    [Trait("page", "110")]
    public void ResolveRangedAttack_LongRangeMiss_SummaryNamesDifficulty()
    {
        var result = _combat.ResolveRangedAttack(Setup(40, 60, RangeLevel.Long));

        Assert.Equal("Стрелок промахивается (40, нужен трудный успех).", result.Summary);
    }

    [Theory]
    [Trait("page", "88")]
    [InlineData(RangeLevel.Base, 97, SuccessLevel.Failure)]
    [InlineData(RangeLevel.Long, 97, SuccessLevel.Fumble)] // нужно 30 — крах уже на 96
    [InlineData(RangeLevel.Long, 95, SuccessLevel.Failure)]
    public void ResolveRangedAttack_FumbleDependsOnRange(RangeLevel range, int roll, SuccessLevel expected)
    {
        var result = _combat.ResolveRangedAttack(Setup(roll, 60, range));

        Assert.Equal(expected, result.AttackerSuccessLevel);
        Assert.False(result.AttackerWins);
    }

    [Theory]
    [Trait("page", "110")]
    [InlineData(1, true)]
    [InlineData(5, false)] // чрезвычайный успех, но не 01
    public void ResolveRangedAttack_ExtremeRange_ImpalesOnlyOnCritical(int roll, bool impales)
    {
        var result = _combat.ResolveRangedAttack(Setup(roll, 60, RangeLevel.Extreme));

        Assert.True(result.AttackerWins);
        Assert.Equal(impales, result.IsImpalingWeapon);
        Assert.Equal(impales ? 2 : 0, result.ExtraDamage);
    }

    [Fact]
    [Trait("page", "101")]
    public void ResolveRangedAttack_ExtremeSuccess_MaxDamagePlusImpale()
    {
        var setup = Setup(10);
        setup.SelectedWeapon = Revolver(malfunction: null);

        var result = _combat.ResolveRangedAttack(setup);

        Assert.True(result.IsExtreme);
        Assert.False(result.IsCritical);
        Assert.Equal(10, result.DamageRolled); // 1D10 на максимум, вписанный бросок урона не нужен
        Assert.Equal(2, result.ExtraDamage);
        Assert.Equal(12, result.RawDamage);
    }

    [Theory]
    [Trait("page", "113")]
    [InlineData(100, null, 100, true)]
    [InlineData(null, "00", 100, true)] // «00» на процентных костях — это 100
    [InlineData(null, "00", 99, false)]
    [InlineData(98, null, 98, true)]
    [InlineData(98, null, 97, false)]
    [InlineData(null, "", 100, false)] // осечки нет
    public void ResolveRangedAttack_Malfunction_RollAtOrAboveThreshold(
        int? threshold, string? text, int roll, bool jams)
    {
        var setup = Setup(roll);
        setup.SelectedWeapon = Revolver(threshold);
        setup.SelectedWeapon.Malfunction = text ?? string.Empty;
        using var _ = ScriptedRandom.Use(jams ? [4] : []);

        var result = _combat.ResolveRangedAttack(setup);

        Assert.Equal(jams, result.IsMalfunction);
        if (!jams) return;
        Assert.False(result.AttackerWins);
        Assert.Equal(4, result.JamRepairRounds);
        Assert.Equal(12, result.DefenderHpAfter);
    }

    /// <summary>
    ///     Осечка, патроны и счётчик очереди меняются при разрешении — до «Применить».
    ///     «Отменить» не возвращает ни патрон, ни исправное оружие.
    /// </summary>
    [Fact]
    [Trait("page", "113")]
    [Trait("finding", "F-C02")]
    public void ResolveRangedAttack_JamAndAmmoChangedBeforeApply()
    {
        var setup = Setup(100);
        setup.SelectedWeapon = Revolver();
        using var _ = ScriptedRandom.Use(4);

        _combat.ResolveRangedAttack(setup);
        _combat.CancelPendingResult();

        Assert.Equal("Револьвер .38", _shooter.JammedWeaponName);
        Assert.Equal(4, _shooter.JamRepairRoundsLeft);
        Assert.Equal(5, _shooter.AmmoLoaded["Револьвер .38"]);
        Assert.Equal(1, _shooter.AttacksThisRound);
        Assert.True(_shooter.HasActedThisRound);
    }

    [Fact]
    [Trait("page", "114")]
    [Trait("finding", "F-C02")]
    public void ResolveRangedAttack_VolleyCountsCheckBeforeApply()
    {
        var setup = Setup(40);
        setup.FiringMode = FiringMode.Volley;
        setup.ShotsFired = 4;

        _combat.ResolveRangedAttack(setup);

        Assert.Equal(1, _shooter.AutofireChecksThisRound);
    }

    [Fact]
    [Trait("page", "111")]
    public void ResolveRangedAttack_SpendsAmmo_ShotsFiredAtLeastOne()
    {
        var setup = Setup(40);
        setup.SelectedWeapon = Revolver(malfunction: null);
        setup.ShotsFired = 0;

        _combat.ResolveRangedAttack(setup);

        Assert.Equal(5, CombatService.GetAmmoLeft(_shooter, setup.SelectedWeapon));
    }

    [Theory]
    [Trait("page", "114")]
    [InlineData(5, false)]
    [InlineData(6, true)]
    public void ResolveRangedAttack_AutofireBeyondCritical_Impossible(int checkIndex, bool impossible)
    {
        var setup = Setup(1);
        setup.FiringMode = FiringMode.Volley;
        setup.AutofireCheckIndex = checkIndex;

        var result = _combat.ResolveRangedAttack(setup);

        Assert.Equal(impossible, result.IsImpossibleShot);
        // Даже 01 не попадает, когда сложность выше критической
        Assert.Equal(!impossible, result.AttackerWins);
        Assert.Equal(SuccessLevel.CriticalSuccess, result.RequiredSuccessLevel);
    }

    [Fact]
    [Trait("page", "112")]
    public void ResolveRangedAttack_FumbleIntoMelee_HitsUnluckiestAlly()
    {
        var lucky = Fighters.Make("Везунчик");
        lucky.Luck = 60;
        var unlucky = Fighters.Make("Неудачник");
        unlucky.Luck = 20;
        var dead = Fighters.Make("Покойник");
        dead.Luck = 1;
        dead.IsDead = true;
        var enemy = Fighters.Make("Глубоководный", side: CombatSide.Enemy);
        enemy.Luck = 0;
        _shooter.Luck = 5;
        foreach (var c in new[] { lucky, unlucky, dead, enemy }) _combat.AddCombatant(c);

        var setup = Setup(100);
        setup.IsFiringIntoMelee = true;

        var result = _combat.ResolveRangedAttack(setup);

        Assert.Equal(SuccessLevel.Fumble, result.AttackerSuccessLevel);
        Assert.False(result.AttackerWins);
        Assert.True(result.HitAllyOnFumble);
        Assert.Equal(unlucky.Id, result.HitAllyId);
        Assert.Equal("Неудачник", result.HitAllyName);
    }

    [Fact]
    [Trait("page", "112")]
    public void ResolveRangedAttack_FumbleIntoMelee_NoAlly_AskKeeper()
    {
        var setup = Setup(100);
        setup.IsFiringIntoMelee = true;

        var result = _combat.ResolveRangedAttack(setup);

        Assert.True(result.HitAllyOnFumble);
        Assert.Null(result.HitAllyId);
        Assert.Contains("выберите пострадавшего", result.Summary);
    }

    [Fact]
    [Trait("page", "112")]
    public void ResolveRangedAttack_FumbleNotIntoMelee_PlainMiss()
    {
        var result = _combat.ResolveRangedAttack(Setup(100));

        Assert.False(result.HitAllyOnFumble);
        Assert.False(result.AttackerWins);
    }

    [Fact]
    [Trait("page", "112")]
    public void FindUnluckiestAlly_SameSideOnly_ExcludesShooterTargetAndDead()
    {
        var neutral = Fighters.Make("Прохожий", side: CombatSide.Neutral);
        neutral.Luck = 1;
        var ally = Fighters.Make("Союзник");
        ally.Luck = 40;
        _combat.AddCombatant(neutral);
        _combat.AddCombatant(ally);

        Assert.Same(ally, _combat.FindUnluckiestAlly(_shooter, _target));
        // Стрелок-враг: союзников-врагов кроме цели нет
        Assert.Null(_combat.FindUnluckiestAlly(_target, _shooter));
    }

    [Fact]
    [Trait("page", "111")]
    public void ResolveRangedAttack_Hit_ConsumesAim()
    {
        _shooter.IsAiming = true;

        var result = _combat.ResolveRangedAttack(Setup(40));

        Assert.True(result.AttackerWins);
        Assert.False(_shooter.IsAiming);
        Assert.Contains("+1 прицеливание", result.Modifiers.Reasons);
    }

    /// <summary>
    ///     Промах прицел не тратит: бонусная кость достаётся и следующему выстрелу, хотя сам код
    ///     говорит «прицеливание сохраняется до выстрела».
    /// </summary>
    [Fact]
    [Trait("page", "111")]
    [Trait("finding", "F-C07")]
    public void ResolveRangedAttack_Miss_KeepsAim()
    {
        _shooter.IsAiming = true;

        var result = _combat.ResolveRangedAttack(Setup(70));

        Assert.False(result.AttackerWins);
        Assert.True(_shooter.IsAiming);
    }
}
