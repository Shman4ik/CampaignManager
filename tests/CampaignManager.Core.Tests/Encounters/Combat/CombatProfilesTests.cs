using CampaignManager.Core.Catalogs;
using CampaignManager.Core.Characters;
using CampaignManager.Core.Encounters;
using static CampaignManager.Core.Tests.Encounters.Combat.Fighters;
using static CampaignManager.Core.Tests.Sheet.Sheets;

namespace CampaignManager.Core.Tests.Encounters.Combat;

/// <summary>
/// Снимок «чем воюет» из листа и статблока. Против v1: навык оружия — по ссылке на справочник, а не эвристикой имён
/// (<c>SkillNameMatcher</c>); проникающее — отметка справочника, а не подстрока «пик» в имени («Пикап» v1 был проникающим).
/// </summary>
public sealed class CombatProfilesTests
{
    [Fact]
    [Trait("page", "66")]
    public void Sheet_AlwaysHasBrawl_WeaponsBySkillReference()
    {
        var sheet = NewSheet(50, Skill(Firearms, 45));
        sheet.Characteristics.Str = 60;
        sheet.Weapons.Add(new SheetWeapon { Name = "Кольт", SkillId = Id(Firearms), Damage = "1D10", Range = "15 метров", Ammo = "6", Malfunction = "100" });
        sheet.Weapons.Add(new SheetWeapon { Name = "Нож", SkillId = Id(Fighting), Damage = "1D4+БкУ", Impaling = true });

        var profile = CombatProfiles.FromSheet(sheet, Catalog);

        Assert.Equal(CombatProfiles.BrawlKey, profile.Attacks[0].Key);
        Assert.Equal(CreatureDamageBonusMode.Full, profile.Attacks[0].DamageBonus);
        var colt = profile.Attacks.Single(a => a.Name == "Кольт");
        Assert.Equal(CombatAttackKind.Ranged, colt.Kind);
        Assert.Equal(45, colt.Skill);
        Assert.True(colt.Impaling); // огнестрел — всегда
        Assert.Equal(CreatureDamageBonusMode.None, colt.DamageBonus);
        Assert.Equal(6, colt.AmmoCapacity);
        Assert.Equal(100, colt.Malfunction);
        var knife = profile.Attacks.Single(a => a.Name == "Нож");
        Assert.Equal(CombatAttackKind.Melee, knife.Kind);
        Assert.True(knife.Impaling);
        Assert.Equal(CreatureDamageBonusMode.Full, knife.DamageBonus);
    }

    [Theory]
    [Trait("page", "106")]
    [InlineData(true, null, CreatureDamageBonusMode.Full)]
    [InlineData(false, null, CreatureDamageBonusMode.None)]
    // «Без бонуса» в данных не переопределяет — иначе незаполненный урон съедал бы БкУ (знание v1)
    [InlineData(true, DamageBonusType.None, CreatureDamageBonusMode.Full)]
    [InlineData(false, DamageBonusType.Half, CreatureDamageBonusMode.Half)]
    [InlineData(false, DamageBonusType.Full, CreatureDamageBonusMode.Full)]
    public void DamageBonusOf_DataOverridesRule(bool melee, DamageBonusType? fromData, CreatureDamageBonusMode expected)
    {
        var expression = fromData is { } bonus ? new DamageExpression { DamageBonus = bonus } : null;

        Assert.Equal(expected, CombatProfiles.DamageBonusOf(melee, expression));
    }

    [Fact]
    [Trait("page", "278")]
    public void Statblock_AttacksAndAttacksPerRound()
    {
        var statblock = new Statblock
        {
            AttacksPerRound = 2,
            Attacks =
            [
                new CreatureAttack { Name = "Когти", SkillValue = 45, Damage = "1D6", DamageBonusMode = CreatureDamageBonusMode.Full },
                new CreatureAttack { Name = "Захват", SkillValue = 55, Kind = CreatureAttackKind.Maneuver },
            ],
        };

        var profile = CombatProfiles.FromStatblock(statblock);

        Assert.Equal(2, profile.AttacksPerRound);
        Assert.Equal(55, profile.FightBack);
        Assert.Equal(CombatAttackKind.Maneuver, profile.Attacks[1].Kind);
    }

    [Fact]
    public void Preview_DoesNotTouchCombatState()
    {
        var a = Make("А");
        var state = Battle(a);
        var resolution = new EncounterResolution
        {
            Effects =
            [
                new EncounterEffect { Kind = EncounterEffectKind.Ammo, ParticipantId = a.Id, Key = "x", Amount = 2 },
                new EncounterEffect { Kind = EncounterEffectKind.Prone, ParticipantId = a.Id, Flag = true },
                new EncounterEffect { Kind = EncounterEffectKind.Damage, ParticipantId = a.Id, Amount = 12 },
            ],
        };

        var lines = EncounterEngine.Preview(state, resolution);

        Assert.Equal(3, lines.Count);
        Assert.Empty(a.Combat.Ammo);
        Assert.False(a.Combat.Prone);
        Assert.False(a.Dead);
        Assert.Contains("мгновенная смерть", lines[2].Note);
    }
}
