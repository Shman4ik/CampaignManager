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

    private static SkillCatalog CombatCatalog()
    {
        var fighting = new SkillDefinition(Guid.NewGuid(), "Ближний бой") { Code = SkillCodes.Fighting, Category = SkillCategory.CombatGeneral };
        var firearms = new SkillDefinition(Guid.NewGuid(), "Стрельба") { Code = SkillCodes.Firearms, Category = SkillCategory.CombatFirearms };
        return new SkillCatalog(
        [
            fighting, firearms,
            new(Guid.NewGuid(), "Ближний бой (драка)") { Code = "skill.fighting.brawl", ParentId = fighting.Id, BaseValue = 25, Category = SkillCategory.CombatGeneral },
            new(Guid.NewGuid(), "Ближний бой (меч)") { Code = "skill.fighting.sword", ParentId = fighting.Id, BaseValue = 20, Category = SkillCategory.CombatGeneral },
            new(Guid.NewGuid(), "Стрельба (пистолет)") { Code = "skill.firearms.handgun", ParentId = firearms.Id, BaseValue = 20, Category = SkillCategory.CombatFirearms },
            new(Guid.NewGuid(), "Стрельба (винтовка/дробовик)") { Code = "skill.firearms.rifle-shotgun", ParentId = firearms.Id, BaseValue = 25, Category = SkillCategory.CombatFirearms },
            new(Guid.NewGuid(), "Стрельба (пулемёт)") { Code = "skill.firearms.machine-gun", ParentId = firearms.Id, BaseValue = 10, Category = SkillCategory.CombatFirearms },
            new(Guid.NewGuid(), "Метание") { Code = "skill.throw", BaseValue = 20, Category = SkillCategory.CombatGeneral },
            new(Guid.NewGuid(), "Внимание") { Code = "skill.spot-hidden", BaseValue = 25 },
        ]);
    }

    [Fact]
    public void CombatSkills_AreSheetSpecialisationsPlusPrintedOnes_WithoutGroupParents()
    {
        var catalog = CombatCatalog();
        var sword = catalog.FindByCode("skill.fighting.sword")!.Id;
        var sheet = NewSheet(50, new SheetSkill { SkillId = sword, Value = 40 }, new SheetSkill { SkillId = catalog.FindByCode("skill.spot-hidden")!.Id, Value = 60 });

        var profile = CombatProfiles.FromSheet(sheet, catalog);

        Assert.Equal(
            ["Ближний бой (драка) 25", "Ближний бой (меч) 40", "Метание 20", "Стрельба (винтовка/дробовик) 25", "Стрельба (пистолет) 20"],
            profile.Skills.Select(s => $"{s.Name} {s.Value}"));
        // Пулемёта на листе нет и на бланке он не напечатан — в «Чем» его нет; драка — уже атака, навыком не повторяется.
        Assert.DoesNotContain(CombatProfiles.CombatSkillsOf(profile, catalog), s => s.Name.Contains("драка", StringComparison.Ordinal));
    }

    [Fact]
    public void Dodge_IsNotAnAttackSkill()
    {
        var catalog = new SkillCatalog(
        [
            new(Guid.NewGuid(), "Уклонение") { Code = SkillCodes.Dodge, BaseValue = 25, Category = SkillCategory.CombatGeneral },
            new(Guid.NewGuid(), "Метание") { Code = "skill.throw", BaseValue = 20, Category = SkillCategory.CombatGeneral },
        ]);

        Assert.False(CombatProfiles.IsCombatSkill(catalog, catalog.FindByCode(SkillCodes.Dodge)!.Id));
        Assert.True(CombatProfiles.IsCombatSkill(catalog, catalog.FindByCode("skill.throw")!.Id));
    }

    [Fact]
    public void BrawlWeaponRow_IsTheSameAttackAsUnarmed_NotASecondOne()
    {
        var catalog = CombatCatalog();
        var brawl = catalog.FindByCode("skill.fighting.brawl")!.Id;
        var sheet = NewSheet(50);
        sheet.Weapons.Add(new SheetWeapon { Name = "Драка", SkillId = brawl, Damage = "1d3 - 1" });
        sheet.Weapons.Add(new SheetWeapon { Name = "Нож", SkillId = brawl, Damage = "1D4" });

        var profile = CombatProfiles.FromSheet(sheet, catalog);

        Assert.Equal(["Драка (без оружия)", "Нож"], profile.Attacks.Select(a => a.Name));
    }

    [Fact]
    public void PickedWeapon_GoesToSnapshotWithSkillOfParticipant_AndSurvivesRefresh()
    {
        var catalog = CombatCatalog();
        var handgun = catalog.FindByCode("skill.firearms.handgun")!.Id;
        var sheet = NewSheet(50, new SheetSkill { SkillId = handgun, Value = 55 });
        var npc = EncounterParticipants.FromSheet(Guid.NewGuid(), CharacterKind.Npc, sheet, catalog);
        var colt = new SheetWeapon { Name = "Кольт .45", SkillId = handgun, Damage = "1D10+2", Range = "15 метров", Ammo = "7", Malfunction = "100" };

        Assert.False(CombatProfiles.PickedWeaponGoesToSheet(npc));
        var attack = CombatProfiles.AddPicked(npc, colt, catalog);

        Assert.True(attack.Picked);
        Assert.Equal(55, attack.Skill);
        Assert.Equal(CombatAttackKind.Ranged, attack.Kind);
        EncounterParticipants.Refresh(npc, sheet, catalog);
        Assert.Contains(npc.Profile.Attacks, a => a.Key == attack.Key);

        // Навыка нет ни в листе, ни в статблоке — база справочника.
        var ghoul = EncounterParticipants.FromStatblock(null, "Гуль", new Statblock { HitPoints = 13 });
        var rifle = new SheetWeapon { Name = "Винтовка", SkillId = catalog.FindByCode("skill.firearms.rifle-shotgun")!.Id, Damage = "2D6+4" };
        Assert.Equal(25, CombatProfiles.AddPicked(ghoul, rifle, catalog).Skill);
    }

    [Fact]
    public void PickedWeapon_ForInvestigator_GoesToSheet()
    {
        var investigator = EncounterParticipants.FromSheet(Guid.NewGuid(), CharacterKind.Player, NewSheet(), Catalog);

        Assert.True(CombatProfiles.PickedWeaponGoesToSheet(investigator));
    }
}
