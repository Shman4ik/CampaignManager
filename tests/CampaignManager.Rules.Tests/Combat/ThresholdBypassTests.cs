using CampaignManager.Web.Components.Features.Combat.Model;
using CampaignManager.Web.Components.Features.Combat.Services;

namespace CampaignManager.Rules.Tests.Combat;

/// <summary>
///     Проверки ВЫН и ИНТ внутри боя считают успех как <c>roll &lt;= value</c>, мимо
///     <see cref="CombatService.CalculateSuccessLevel(int, int)" />. Пока значение в 1..99, итог
///     совпадает; расходятся края: 100 при значении от 100 (у тварей ВЫН бывает выше) засчитывается
///     успехом, а 01 при нуле — провалом.
/// </summary>
[Trait("finding", "F-C06")]
public sealed class ThresholdBypassTests
{
    [Theory]
    [Trait("page", "117")]
    [Trait("page", "87")]
    // ВЫН, бросок, итог v1, итог по CalculateSuccessLevel
    [InlineData(120, 100, true, false)] // книга: 100 — всегда крах
    [InlineData(100, 100, true, false)]
    [InlineData(0, 1, false, true)] // книга: 01 — всегда критический успех
    [InlineData(50, 96, false, false)] // в обычном диапазоне итоги совпадают
    [InlineData(50, 50, true, true)]
    public void MajorWoundConRoll_RollAtOrBelowValue(int constitution, int conRoll, bool conscious, bool byLevel)
    {
        var attacker = Fighters.Make("Сыщик");
        var defender = Fighters.Make("Шоггот", hp: 40, side: CombatSide.Enemy);
        defender.ConstitutionValue = constitution;
        var combat = Fighters.Battle(attacker, defender);

        var result = combat.ResolveMeleeAttack(new AttackSetup
        {
            AttackerId = attacker.Id,
            DefenderId = defender.Id,
            IsMelee = true,
            AttackSkillValue = 50,
            SurpriseMode = SurpriseMode.BonusDie,
            ManualAttackerRoll = 40,
            ManualWeaponDamageRoll = 20,
            ManualDamageBonusRoll = 0,
            ManualMajorWoundConRoll = conRoll
        });

        Assert.True(result.TriggeredMajorWound);
        Assert.Equal(conscious, result.MajorWoundConRollSuccess);
        Assert.Equal(!conscious, result.DefenderKnockedUnconscious);
        Assert.Equal(byLevel, CombatService.CalculateSuccessLevel(conRoll, constitution) >= SuccessLevel.RegularSuccess);
    }

    [Theory]
    [Trait("page", "153")]
    [InlineData(100, 100, true)]
    [InlineData(0, 1, false)]
    public void SanityIntRoll_RollAtOrBelowValue(int intelligence, int intRoll, bool realized)
    {
        var investigator = Fighters.Investigator("Сыщик", sanity: 50, intelligence: intelligence);
        var combat = Fighters.Battle(investigator);

        var result = combat.ResolveSanityCheck(new SanityCheckSetup
        {
            TargetId = investigator.Id,
            ManualRoll = 70,
            FailureLoss = "5",
            ManualIntRoll = intRoll,
            ManualInsanityDurationRoll = 3
        });

        Assert.Equal(realized, result.TriggeredTemporaryInsanity);
    }

    [Theory]
    [Trait("page", "176")]
    [InlineData(120, 100, true)]
    [InlineData(0, 1, false)]
    public void SpellShortfallConRoll_RollAtOrBelowValue(int constitution, int conRoll, bool conscious)
    {
        var caster = Fighters.Make("Жрец", side: CombatSide.Enemy);
        caster.ConstitutionValue = constitution;
        var combat = Fighters.Battle(caster);

        var result = combat.ResolveSpellCast(new SpellCastSetup
        {
            CasterId = caster.Id,
            SpellName = "Иссушение",
            MagicPointsCost = 6,
            ManualMajorWoundConRoll = conRoll
        });

        Assert.True(result.AttackerTriggeredMajorWound);
        Assert.Equal(conscious, result.AttackerMajorWoundConRollSuccess);
    }
}
