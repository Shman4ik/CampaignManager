using CampaignManager.Core.Catalogs;
using CampaignManager.Core.Characters;
using CampaignManager.Core.Encounters;
using CampaignManager.Core.Tests.Infrastructure;
using static CampaignManager.Core.Tests.Encounters.Combat.Fighters;

namespace CampaignManager.Core.Tests.Encounters.Combat;

/// <summary>
/// Урон попавшей атаки (перенесено из T0.2 <c>DamageTests</c>): максимум при чрезвычайном успехе, бонус к урону, броня и
/// укрытие, мгновенная смерть, серьёзная рана. Ближний бой с вписанными бросками, цель не защищается.
/// </summary>
public sealed class DamageTests
{
    private readonly EncounterParticipant _attacker = Make("Сыщик");
    private readonly EncounterParticipant _defender = Make("Культист", side: EncounterSide.Enemies);
    private readonly EncounterState _state;

    public DamageTests() => _state = Battle(_attacker, _defender);

    private MeleeAttackSetup Hit(int attackRoll = 40, int damage = 3) => new()
    {
        AttackerId = _attacker.Id,
        DefenderId = _defender.Id,
        AttackSkill = 50,
        Surprise = SurpriseMode.BonusDie,
        AttackRoll = Rolled(attackRoll),
        DamageRoll = damage,
        DamageBonusRoll = 0,
        ConRoll = Rolled(10),
    };

    private AttackOutcome Resolve(MeleeAttackSetup setup, ScriptedDice? dice = null) => CombatRules.Melee(_state, setup, dice ?? ScriptedDice.Of());

    private void Arm(CombatAttack attack) => _attacker.Profile.Attacks.Add(attack);

    [Theory]
    [Trait("page", "101")]
    [InlineData(10, false)] // чрезвычайный
    [InlineData(1, true)] // критический
    public void ExtremeSuccess_MaxWeaponAndMaxDamageBonus_IgnoresEnteredRolls(int roll, bool critical)
    {
        _attacker.Stats.DamageBonus = "+1D4";
        var setup = Hit(roll, damage: 1) with { DamageBonusRoll = 1 };

        var outcome = Resolve(setup);

        Assert.True(outcome.Damage!.Extreme);
        Assert.Equal(critical, outcome.Damage.Critical);
        Assert.Equal(3, outcome.Damage.Rolled); // драка 1D3 — на максимум
        Assert.Equal(4, outcome.Damage.Bonus);
        Assert.Equal(0, outcome.Damage.Extra); // кулак не проникающий
        Assert.Equal(7, outcome.Damage.Total);
    }

    [Fact]
    [Trait("page", "101")]
    public void ExtremeSuccess_ImpalingWeapon_AddsExtraRoll()
    {
        Arm(new CombatAttack { Key = "knife", Name = "Нож", Damage = "1D4", Impaling = true, DamageBonus = CreatureDamageBonusMode.Full });
        var setup = Hit(10) with { AttackKey = "knife" };

        var outcome = Resolve(setup, ScriptedDice.Of(3));

        Assert.True(outcome.Damage!.Impaling);
        Assert.Equal(4, outcome.Damage.Rolled);
        Assert.Equal(3, outcome.Damage.Extra); // дополнительный бросок оружия, без БкУ
        Assert.Equal(7, outcome.Damage.Raw);
        Assert.Contains("проникающая", outcome.Damage.Describe());
    }

    [Theory]
    // «Урон: 1d3 = 2, бонус к урону −1 → 1.»: строчная d, слово вместо «БкУ», настоящий минус.
    [InlineData("1D3", 2, -1, 0, 0, 1, "Урон: 1d3 = 2, бонус к урону −1 → 1.")]
    [InlineData("2d6", 4, 4, 0, 0, 8, "Урон: 2d6 = 4, бонус к урону +4 → 8.")]
    [InlineData("1d8", 5, 0, 0, 2, 3, "Урон: 1d8 = 5, броня −2 → 3.")]
    [InlineData("1d10", 6, 0, 0, 0, 6, "Урон: 1d10 = 6.")]
    public void Describe_IsPlainRussianWithoutAbbreviations(string formula, int rolled, int bonus, int extra, int armor, int total, string expected)
    {
        var damage = new DamageRoll(formula, rolled, bonus, extra, rolled + bonus + extra, armor, total, false, false, false);

        Assert.Equal(expected, damage.Describe());
    }

    [Theory]
    [Trait("page", "106")]
    [InlineData(false, 4, 4, 6)]
    [InlineData(true, 4, 2, 3)] // половинный БкУ — с округлением вниз, и максимум тоже делится
    [InlineData(true, 5, 2, 3)]
    public void DamageBonus_HalfFromWeapon(bool half, int rolledBonus, int expectedRegular, int expectedExtreme)
    {
        _attacker.Stats.DamageBonus = "+1D6";
        Arm(new CombatAttack
        {
            Key = "sling", Name = "Праща", Damage = "1D3",
            DamageBonus = half ? CreatureDamageBonusMode.Half : CreatureDamageBonusMode.Full,
        });

        var regular = Resolve(Hit() with { AttackKey = "sling", DamageBonusRoll = rolledBonus });
        var extreme = Resolve(Hit(10) with { AttackKey = "sling" });

        Assert.Equal(expectedRegular, regular.Damage!.Bonus);
        Assert.Equal(expectedExtreme, extreme.Damage!.Bonus);
    }

    [Theory]
    [Trait("page", "278")]
    [InlineData(CreatureDamageBonusMode.None, 0)]
    [InlineData(CreatureDamageBonusMode.Full, 4)]
    [InlineData(CreatureDamageBonusMode.Half, 2)]
    [InlineData(CreatureDamageBonusMode.OnlyBonus, 4)]
    public void CreatureAttack_DamageBonusModeFromStatblock(CreatureDamageBonusMode mode, int expectedBonus)
    {
        _attacker.Stats.DamageBonus = "+1D6";
        Arm(new CombatAttack
        {
            Key = "bite", Name = "Укус", Damage = mode == CreatureDamageBonusMode.OnlyBonus ? "0" : "1D6", DamageBonus = mode,
        });

        var outcome = Resolve(Hit() with { AttackKey = "bite", DamageBonusRoll = 4 });

        Assert.Contains("Укус", outcome.Summary);
        Assert.Equal(expectedBonus, outcome.Damage!.Bonus);
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
        _defender.Stats.Armor = armor;

        var outcome = Resolve(Hit(damage: damage) with { CoverArmor = coverArmor, IgnoresArmor = ignores });

        Assert.Equal(damage, outcome.Damage!.Raw);
        Assert.Equal(reduction, outcome.Damage.Armor);
        Assert.Equal(total, outcome.Damage.Total);
    }

    [Fact]
    [Trait("page", "106")]
    public void NegativeDamageRoll_RawDamageClampedToZero_NoDamageEffect()
    {
        var outcome = Resolve(Hit(damage: -2));

        Assert.Equal(0, outcome.Damage!.Raw);
        Assert.DoesNotContain(outcome.Resolution.Effects, e => e.Kind == EncounterEffectKind.Damage);
    }

    /// <summary>Мгновенная смерть: урон одной атаки ≥ максимума ПЗ, граница «равно» — смерть (F-S02, решение владельца).</summary>
    [Theory]
    [Trait("page", "118")]
    [Trait("finding", "F-S02")]
    [InlineData(12, true)]
    [InlineData(15, true)]
    [InlineData(11, false)]
    public void InstantDeath_DamageAtLeastMaxHp(int damage, bool dies)
    {
        _defender.HitPoints = 20; // не важно, сколько осталось — сравнение с максимумом

        var outcome = Resolve(Hit(damage: damage));
        Apply(_state, outcome);

        Assert.Equal(dies, outcome.Wound!.InstantDeath);
        Assert.Equal(dies, _defender.Dead);
        if (dies)
        {
            Assert.Equal(0, _defender.HitPoints);
            Assert.Contains("мгновенная смерть", outcome.Summary);
        }
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
        _defender.HitPoints = maxHp;

        var outcome = Resolve(Hit(damage: damage));

        Assert.Equal(major, outcome.Wound!.MajorWound);
        Assert.Equal(major, outcome.Wound.FallsProne);
        Assert.Equal(major ? true : null, outcome.Wound.ConPassed);
        Assert.Equal(WoundRules.IsMajorWound(damage, maxHp), outcome.Wound.MajorWound);
    }

    [Theory]
    [Trait("page", "117")]
    [InlineData(50, true)]
    [InlineData(51, false)]
    public void MajorWound_ConRollDecidesConsciousness(int conRoll, bool conscious)
    {
        var outcome = Resolve(Hit(damage: 6) with { ConRoll = Rolled(conRoll) });
        Apply(_state, outcome);

        Assert.Equal(conscious, outcome.Wound!.ConPassed);
        Assert.Equal(!conscious, _defender.Unconscious);
        Assert.False(_defender.Dying);
        Assert.True(_defender.Combat.Prone); // серьёзная рана валит с ног
    }

    [Fact]
    [Trait("page", "117")]
    public void MajorWound_NoEnteredConRoll_RollsD100()
    {
        // ВЫН 50, бросок 77 (единицы 7, десятки 7)
        var outcome = Resolve(Hit(damage: 6) with { ConRoll = null }, ScriptedDice.Of(7, 7));

        Assert.False(outcome.Wound!.ConPassed);
        Assert.True(outcome.Wound.After.Unconscious);
    }

    [Theory]
    [Trait("page", "117-118")]
    // 0 ПЗ без серьёзной раны — без сознания; с раной (новой или прежней) — при смерти
    [InlineData(3, 4, false, false)]
    [InlineData(3, 4, true, true)]
    [InlineData(5, 6, false, true)]
    public void ZeroHp_DyingOnlyWithMajorWound(int currentHp, int damage, bool hadMajorWound, bool dying)
    {
        _defender.HitPoints = currentHp;
        _defender.MajorWound = hadMajorWound;

        Apply(_state, Resolve(Hit(damage: damage)));

        Assert.Equal(0, _defender.HitPoints);
        Assert.Equal(dying, _defender.Dying);
        Assert.True(_defender.Unconscious);
        Assert.False(_defender.Dead);
    }
}
