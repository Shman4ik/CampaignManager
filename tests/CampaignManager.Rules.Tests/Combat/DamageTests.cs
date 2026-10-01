using CampaignManager.Rules.Tests.Infrastructure;
using CampaignManager.Web.Components.Features.Bestiary.Model;
using CampaignManager.Web.Components.Features.Characters.Services;
using CampaignManager.Web.Components.Features.Combat.Model;
using CampaignManager.Web.Components.Features.Combat.Services;
using CampaignManager.Web.Components.Features.Weapons.Model;
using CampaignManager.Web.Model;

namespace CampaignManager.Rules.Tests.Combat;

/// <summary>
///     Урон попавшей атаки: максимум при чрезвычайном успехе, бонус к урону, броня и укрытие,
///     мгновенная смерть, серьёзная рана. Атака — ближний бой с вписанными бросками, цель не защищается.
/// </summary>
public sealed class DamageTests
{
    private readonly Combatant _attacker = Fighters.Make("Сыщик");
    private readonly Combatant _defender = Fighters.Make("Культист", side: CombatSide.Enemy);
    private readonly CombatService _combat;

    public DamageTests() => _combat = Fighters.Battle(_attacker, _defender);

    private AttackSetup Hit(int attackRoll = 40, int damage = 3) => new()
    {
        AttackerId = _attacker.Id,
        DefenderId = _defender.Id,
        IsMelee = true,
        AttackSkillValue = 50,
        SurpriseMode = SurpriseMode.BonusDie,
        ManualAttackerRoll = attackRoll,
        ManualWeaponDamageRoll = damage,
        ManualDamageBonusRoll = 0,
        ManualMajorWoundConRoll = 10
    };

    [Theory]
    [Trait("page", "101")]
    [InlineData(10, false)] // чрезвычайный
    [InlineData(1, true)] // критический
    public void ExtremeSuccess_MaxWeaponAndMaxDamageBonus_IgnoresManualRolls(int roll, bool critical)
    {
        _attacker.DamageBonus = "+1D4";
        var setup = Hit(roll, damage: 1);
        setup.ManualDamageBonusRoll = 1;

        var result = _combat.ResolveMeleeAttack(setup);

        Assert.True(result.IsExtreme);
        Assert.Equal(critical, result.IsCritical);
        Assert.Equal(3, result.DamageRolled); // 1D3 без оружия — на максимум
        Assert.Equal(4, result.BonusDamage);
        Assert.Equal(0, result.ExtraDamage); // кулак не проникающий
        Assert.Equal(7, result.TotalDamage);
    }

    [Fact]
    [Trait("page", "101")]
    public void ExtremeSuccess_ImpalingWeapon_AddsExtraRoll()
    {
        var setup = Hit(10);
        setup.SelectedWeapon = new Weapon { Name = "Нож", Type = WeaponType.Melee, Damage = "1D4" };
        using var _ = ScriptedRandom.Use(3);

        var result = _combat.ResolveMeleeAttack(setup);

        Assert.True(result.IsImpalingWeapon);
        Assert.Equal(4, result.DamageRolled);
        Assert.Equal(3, result.ExtraDamage); // дополнительный бросок оружия, без БкУ
        Assert.Equal(7, result.RawDamage);
        Assert.Contains("проникающая рана", result.Summary);
    }

    [Fact]
    [Trait("page", "101")]
    public void ExtremeSuccess_StructuredDamage_PreferredOverText()
    {
        var setup = Hit(10);
        setup.SelectedWeapon = new Weapon
        {
            Name = "Дубинка",
            Type = WeaponType.Melee,
            Damage = "1D3",
            DamageInfo = new WeaponDamageInfo
            {
                Primary = new DamageExpression { Dice = [new DiceTerm(1, 8)], FlatModifier = 1, IsParsed = true }
            }
        };

        var result = _combat.ResolveMeleeAttack(setup);

        Assert.Equal(9, result.DamageRolled);
        Assert.Equal("1D3", result.DamageFormula);
    }

    [Theory]
    [Trait("page", "106")]
    [InlineData(false, 4, 4, 6)]
    [InlineData(true, 4, 2, 3)] // половинный БкУ — с округлением вниз, и максимум тоже делится
    [InlineData(true, 5, 2, 3)]
    public void DamageBonus_HalfFromWeaponData(bool half, int rolledBonus, int expectedRegular, int expectedExtreme)
    {
        _attacker.DamageBonus = "+1D6";
        var weapon = new Weapon
        {
            Name = "Праща",
            Type = WeaponType.Melee,
            Damage = "1D3",
            DamageInfo = new WeaponDamageInfo
            {
                Primary = new DamageExpression
                {
                    Dice = [new DiceTerm(1, 3)],
                    DamageBonus = half ? DamageBonusType.Half : DamageBonusType.Full,
                    IsParsed = true
                }
            }
        };

        var regular = Hit();
        regular.SelectedWeapon = weapon;
        regular.ManualDamageBonusRoll = rolledBonus;
        var extreme = Hit(10);
        extreme.SelectedWeapon = weapon;

        Assert.Equal(expectedRegular, _combat.ResolveMeleeAttack(regular).BonusDamage);
        Assert.Equal(expectedExtreme, _combat.ResolveMeleeAttack(extreme).BonusDamage);
    }

    [Theory]
    [Trait("page", "278")]
    [InlineData(CreatureDamageBonusMode.None, 0)]
    [InlineData(CreatureDamageBonusMode.Full, 4)]
    [InlineData(CreatureDamageBonusMode.Half, 2)]
    [InlineData(CreatureDamageBonusMode.OnlyBonus, 4)]
    public void CreatureAttack_DamageBonusModeFromStatBlock(CreatureDamageBonusMode mode, int expectedBonus)
    {
        _attacker.DamageBonus = "+1D6";
        var setup = Hit();
        setup.CreatureAttackName = "Укус";
        setup.CreatureAttackDamage = mode == CreatureDamageBonusMode.OnlyBonus ? "0" : "1D6";
        setup.CreatureDamageBonus = mode;
        setup.ManualDamageBonusRoll = 4;

        var result = _combat.ResolveMeleeAttack(setup);

        Assert.Equal("Укус", result.WeaponName);
        Assert.Equal(expectedBonus, result.BonusDamage);
    }

    [Theory]
    [Trait("page", "106")]
    [Trait("page", "125")]
    [InlineData(5, 2, 0, false, 2, 3)]
    [InlineData(5, 2, 3, false, 5, 0)] // броня преграды складывается с бронёй цели
    [InlineData(5, 0, 2, false, 2, 3)]
    [InlineData(5, 2, -3, false, 2, 3)] // отрицательная броня укрытия не считается
    [InlineData(5, 2, 3, true, 0, 5)] // магия, яд, утопление — мимо брони
    [InlineData(1, 4, 0, false, 1, 0)]
    public void Armor_ReducesDamage_NotBelowZero(int damage, int armor, int coverArmor, bool ignores, int reduction, int total)
    {
        _defender.Armor = armor;
        var setup = Hit(damage: damage);
        setup.CoverArmor = coverArmor;
        setup.IgnoresArmor = ignores;

        var result = _combat.ResolveMeleeAttack(setup);

        Assert.Equal(damage, result.RawDamage);
        Assert.Equal(reduction, result.ArmorReduction);
        Assert.Equal(total, result.TotalDamage);
    }

    [Fact]
    [Trait("page", "106")]
    public void NegativeDamageRoll_RawDamageClampedToZero()
    {
        var result = _combat.ResolveMeleeAttack(Hit(damage: -2));

        Assert.Equal(0, result.RawDamage);
        Assert.Equal(12, result.DefenderHpAfter);
    }

    [Theory]
    [Trait("page", "118")]
    [InlineData(12, true)]
    [InlineData(15, true)]
    [InlineData(11, false)]
    public void InstantDeath_DamageAtLeastMaxHp(int damage, bool dies)
    {
        _defender.CurrentHitPoints = 20; // не важно, сколько осталось — сравнение с максимумом
        var setup = Hit(damage: damage);
        setup.ManualMajorWoundConRoll = 10;

        var result = _combat.ResolveMeleeAttack(setup);

        Assert.Equal(dies, result.IsInstantDeath);
        Assert.Equal(dies, result.DefenderDead);
        if (dies) Assert.Equal(0, result.DefenderHpAfter);
    }

    [Theory]
    [Trait("page", "117")]
    [InlineData(12, 6, true)]
    [InlineData(12, 5, false)]
    [InlineData(13, 7, true)] // половина 6,5 — серьёзная с 7
    [InlineData(13, 6, false)]
    [InlineData(3, 2, true)]
    public void MajorWound_DamageAtLeastHalfMaxHp(int maxHp, int damage, bool major)
    {
        _defender.MaxHitPoints = maxHp;
        _defender.CurrentHitPoints = maxHp;
        var setup = Hit(damage: damage);
        setup.ManualMajorWoundConRoll = 10;

        var result = _combat.ResolveMeleeAttack(setup);

        Assert.Equal(major, result.TriggeredMajorWound);
        Assert.Equal(major, result.DefenderFallsProne);
        Assert.Equal(major ? 10 : (int?)null, result.MajorWoundConRoll);
        // Инлайн-порог боя совпадает с общим WoundRules
        Assert.Equal(WoundRules.IsMajorWound(damage, maxHp), result.TriggeredMajorWound);
    }

    [Theory]
    [Trait("page", "117")]
    [InlineData(50, true)]
    [InlineData(51, false)]
    public void MajorWound_ConRollDecidesConsciousness(int conRoll, bool conscious)
    {
        var setup = Hit(damage: 6);
        setup.ManualMajorWoundConRoll = conRoll;

        var result = _combat.ResolveMeleeAttack(setup);

        Assert.Equal(conscious, result.MajorWoundConRollSuccess);
        Assert.Equal(!conscious, result.DefenderKnockedUnconscious);
        Assert.False(result.DefenderDying);
    }

    [Fact]
    [Trait("page", "117")]
    public void MajorWound_NoManualConRoll_RollsD100()
    {
        var setup = Hit(damage: 6);
        setup.ManualMajorWoundConRoll = null;
        using var _ = ScriptedRandom.Use(77);

        var result = _combat.ResolveMeleeAttack(setup);

        Assert.Equal(77, result.MajorWoundConRoll);
        Assert.True(result.DefenderKnockedUnconscious);
    }

    [Theory]
    [Trait("page", "117-118")]
    // 0 ПЗ без серьёзной раны — без сознания; с раной (новой или прежней) — при смерти
    [InlineData(3, 4, false, false, true)]
    [InlineData(3, 4, true, true, false)]
    [InlineData(5, 6, false, true, false)]
    public void ZeroHp_DyingOnlyWithMajorWound(int currentHp, int damage, bool hadMajorWound, bool dying, bool unconscious)
    {
        _defender.CurrentHitPoints = currentHp;
        _defender.HasMajorWound = hadMajorWound;
        var setup = Hit(damage: damage);
        setup.ManualMajorWoundConRoll = 10; // ВЫН пройдена — сознание теряется только от 0 ПЗ

        var result = _combat.ResolveMeleeAttack(setup);

        Assert.Equal(0, result.DefenderHpAfter);
        Assert.Equal(dying, result.DefenderDying);
        Assert.Equal(unconscious, result.DefenderKnockedUnconscious);
    }
}
