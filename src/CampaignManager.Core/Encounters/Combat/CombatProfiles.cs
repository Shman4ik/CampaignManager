using System.Globalization;
using CampaignManager.Core.Catalogs;
using CampaignManager.Core.Characters;

namespace CampaignManager.Core.Encounters;

/// <summary>
/// Снимок «чем воюет» (<see cref="CombatProfile"/>) из листа и статблока. Навык оружия — по ссылке на справочник
/// (<see cref="SheetWeapon.SkillId"/>), без сопоставления имён: эвристика v1 (<c>SkillNameMatcher</c>) была нужна
/// оторванным копиям оружия в JSONB, а перенос T1.3 проставил ссылки. Числа оружия — <see cref="WeaponStatsReader"/>
/// из текста книги на листе.
/// </summary>
public static class CombatProfiles
{
    /// <summary>Ключ безоружной атаки: она есть у каждого листа (стр. 66: Ближний бой (драка), урон 1D3 + БкУ).</summary>
    public const string BrawlKey = "brawl";

    public const string BrawlDamage = "1D3";

    public static CombatProfile FromSheet(CharacterSheet sheet, SkillCatalog catalog)
    {
        var brawl = sheet.Value(catalog, SkillCodes.Fighting + ".brawl");
        List<CombatAttack> attacks =
        [
            new()
            {
                Key = BrawlKey,
                Name = "Драка (без оружия)",
                Skill = brawl,
                Damage = BrawlDamage,
                Kind = CombatAttackKind.Melee,
                DamageBonus = CreatureDamageBonusMode.Full,
            },
        ];

        foreach (var weapon in sheet.Weapons)
            attacks.Add(FromWeapon(weapon, sheet, catalog));

        return new CombatProfile
        {
            Attacks = attacks,
            AttacksPerRound = 1,
            FightBack = Math.Max(brawl, attacks.Where(a => a.Kind == CombatAttackKind.Melee).Select(a => a.Skill).DefaultIfEmpty(0).Max()),
            FirstAid = sheet.Value(catalog, "skill.first-aid"),
            Medicine = sheet.Value(catalog, "skill.medicine"),
            MechanicalRepair = sheet.Value(catalog, "skill.mechanical-repair"),
            Spells =
            [
                .. sheet.Spells.Select(s => new CombatSpell
                {
                    CatalogSpellId = s.CatalogSpellId, Name = s.Name, Cost = s.Cost, CastingTime = s.CastingTime,
                }),
            ],
            Skills = CombatSkillsOf(sheet, catalog),
        };
    }

    /// <summary>Навыки, которые напечатаны на бланке сыщика, — они есть у каждого, даже без вложенных очков.</summary>
    private static readonly string[] PrintedCombatSkills = [SkillCodes.Fighting + ".brawl", "skill.throw", SkillCodes.Firearms + ".handgun", SkillCodes.Firearms + ".rifle-shotgun"];

    /// <summary>Боевой навык справочника — ближний бой, стрельба, метание (категории «Сражение»), без родителей-групп.</summary>
    public static bool IsCombatSkill(SkillCatalog catalog, Guid skillId) =>
        catalog.Find(skillId) is { Category: SkillCategory.CombatGeneral or SkillCategory.CombatFirearms } skill && !catalog.IsParent(skill.Id);

    /// <summary>
    /// Боевые навыки листа для «Чем»: каждая специализация, что есть на листе, плюс напечатанные на листе книги (драка,
    /// метание, пистолет, винтовка) — с базой, если очков не вкладывали. Порядок — по имени.
    /// </summary>
    public static List<CombatSkill> CombatSkillsOf(CharacterSheet sheet, SkillCatalog catalog)
    {
        var ids = sheet.Skills.Where(s => s.SkillId is { } id && IsCombatSkill(catalog, id)).Select(s => s.SkillId!.Value)
            .Concat(PrintedCombatSkills.Select(code => catalog.FindByCode(code)?.Id).OfType<Guid>())
            .Distinct();

        return
        [
            .. ids.Select(id => catalog.Find(id)!).Select(skill => new CombatSkill
            {
                SkillId = skill.Id,
                Name = skill.Name,
                Value = sheet.Entry(skill.Id)?.Value ?? SkillCatalog.BaseValueOf(skill, sheet.Characteristics),
            }).OrderBy(s => s.Name, StringComparer.Ordinal),
        ];
    }

    /// <summary>
    /// Боевые навыки участника для «Чем» (снимок листа уже отобран, у статблока — его навыки боевых категорий).
    /// Драки здесь нет: она уже атака (<see cref="BrawlKey"/>).
    /// </summary>
    public static IReadOnlyList<CombatSkill> CombatSkillsOf(CombatProfile profile, SkillCatalog catalog) =>
        [.. profile.Skills.Where(s => IsCombatSkill(catalog, s.SkillId) && catalog.CodeOf(s.SkillId) != SkillCodes.Fighting + ".brawl")];

    /// <summary>
    /// Значение навыка оружия у участника: из снимка (лист, статблок), а если навыка там нет — база справочника
    /// (у боевых навыков она число: драка 25, пистолет 20…).
    /// </summary>
    public static int SkillValueOf(CombatProfile profile, SkillCatalog catalog, Guid skillId) =>
        profile.Skills.FirstOrDefault(s => s.SkillId == skillId)?.Value
        ?? (catalog.CodeOf(skillId) == SkillCodes.Fighting + ".brawl" ? profile.Attacks.FirstOrDefault(a => a.Key == BrawlKey)?.Skill : null)
        ?? catalog.Find(skillId)?.BaseValue
        ?? 0;

    /// <summary>
    /// Оружие, подобранное в сцене участнику без листа (НПС, тварь): атака только в снимке (<see cref="CombatAttack.Picked"/>).
    /// Числа — те же, что у оружия листа (<see cref="FromWeapon(SheetWeapon, int, int, SkillCatalog)"/>); навык — из снимка.
    /// </summary>
    public static CombatAttack Picked(SheetWeapon weapon, CombatProfile profile, int strength, SkillCatalog catalog) =>
        FromWeapon(weapon, weapon.SkillId is { } id ? SkillValueOf(profile, catalog, id) : 0, strength, catalog) with { Picked = true };

    /// <summary>
    /// Куда идёт подобранное оружие (решение владельца 2026-10-03): сыщику — в лист, в снаряжение (остаётся после боя);
    /// НПС и твари — только в снимок этой сцены.
    /// </summary>
    public static bool PickedWeaponGoesToSheet(EncounterParticipant participant) =>
        participant is { Kind: ParticipantKind.Investigator, SourceCharacterId: not null };

    /// <summary>Подобранное оружие в снимок участника (НПС, тварь); ключ атаки — строка оружия. Возвращает атаку.</summary>
    public static CombatAttack AddPicked(EncounterParticipant participant, SheetWeapon weapon, SkillCatalog catalog)
    {
        var attack = Picked(weapon, participant.Profile, participant.Stats.Str, catalog);
        participant.Profile.Attacks.Add(attack);
        return attack;
    }

    /// <summary>Оружие листа как атака: дальний бой — по навыку (Стрельба, Метание) или по дальности в метрах.</summary>
    public static CombatAttack FromWeapon(SheetWeapon weapon, CharacterSheet sheet, SkillCatalog catalog)
    {
        var skill = weapon.SkillId is { } skillId
            ? sheet.Entry(skillId)?.Value ?? (catalog.Find(skillId) is { } definition ? SkillCatalog.BaseValueOf(definition, sheet.Characteristics) : 0)
            : 0;
        return FromWeapon(weapon, skill, sheet.Characteristics.Str, catalog);
    }

    /// <summary>Оружие как атака с уже известным значением навыка и СИЛ (метание на «СИЛ/5 м»).</summary>
    public static CombatAttack FromWeapon(SheetWeapon weapon, int skill, int strength, SkillCatalog catalog)
    {
        var code = catalog.CodeOf(weapon.SkillId);
        var parentCode = catalog.CodeOf(catalog.Find(weapon.SkillId)?.ParentId);
        var firearm = IsCode(code, SkillCodes.Firearms) || IsCode(parentCode, SkillCodes.Firearms);
        var thrown = code == "skill.throw";
        var range = WeaponStatsReader.Range(weapon);
        var ranged = firearm || thrown
                     || (code is null && range.Kind is WeaponRangeKind.Meters or WeaponRangeKind.RangeBands or WeaponRangeKind.StrengthThrow);

        var expression = WeaponStatsReader.Damage(weapon).GetDefaultDamage();
        var attacks = WeaponStatsReader.Attacks(weapon);

        return new CombatAttack
        {
            Key = weapon.RowId.ToString("N", CultureInfo.InvariantCulture),
            Name = string.IsNullOrWhiteSpace(weapon.Name) ? "Оружие" : weapon.Name.Trim(),
            Skill = skill,
            Damage = weapon.Damage,
            Kind = ranged ? CombatAttackKind.Ranged : CombatAttackKind.Melee,
            DamageBonus = DamageBonusOf(!ranged, expression),
            // Огнестрел — всегда проникающий (стр. 101); холодное и метательное — по отметке справочника.
            Impaling = firearm || weapon.Impaling,
            AmmoCapacity = WeaponStatsReader.AmmoCapacity(weapon),
            Malfunction = WeaponStatsReader.TryMalfunctionThreshold(weapon, out var malfunction) ? malfunction : null,
            BaseRangeMeters = WeaponStatsReader.BaseRangeMeters(weapon)
                              ?? (range.Kind == WeaponRangeKind.StrengthThrow && range.ThrowDivisor is > 0 and var divisor
                                  ? strength / divisor
                                  : null),
            ShotsPerRound = attacks.MaxShotsPerRound ?? attacks.ShotsPerRound,
            Automatic = attacks.AllowsFullAuto || attacks.AllowsBurst,
            Description = string.IsNullOrWhiteSpace(weapon.Notes) ? null : weapon.Notes,
        };
    }

    /// <summary>
    /// Бонус к урону оружия (стр. 106): по умолчанию ближний бой — полный, дальний — нет. Формула оружия может сказать иное
    /// («+ ½ БкУ» у метательного); «без бонуса» в данных переопределением не считается — иначе незаполненный урон съедал
    /// бы БкУ (знание v1, <c>ResolveDamageBonusType</c>).
    /// </summary>
    public static CreatureDamageBonusMode DamageBonusOf(bool melee, DamageExpression? expression) => expression?.DamageBonus switch
    {
        DamageBonusType.Full => CreatureDamageBonusMode.Full,
        DamageBonusType.Half => CreatureDamageBonusMode.Half,
        _ => melee ? CreatureDamageBonusMode.Full : CreatureDamageBonusMode.None,
    };

    public static CombatProfile FromStatblock(Statblock statblock)
    {
        List<CombatAttack> attacks =
        [
            .. statblock.Attacks.Select((a, index) => new CombatAttack
            {
                Key = $"a{index.ToString(CultureInfo.InvariantCulture)}",
                Name = string.IsNullOrWhiteSpace(a.Name) ? "Атака" : a.Name.Trim(),
                Skill = a.SkillValue,
                Damage = a.Damage,
                Kind = a.Kind switch
                {
                    CreatureAttackKind.Ranged => CombatAttackKind.Ranged,
                    CreatureAttackKind.Maneuver => CombatAttackKind.Maneuver,
                    CreatureAttackKind.Special => CombatAttackKind.Special,
                    _ => CombatAttackKind.Melee,
                },
                DamageBonus = a.DamageBonusMode,
                Impaling = a.Kind == CreatureAttackKind.Ranged,
                Description = a.Description,
            }),
        ];

        return new CombatProfile
        {
            Attacks = attacks,
            AttacksPerRound = Math.Max(1, statblock.AttacksPerRound),
            Skills = [.. statblock.Skills.Where(s => s.SkillId is not null).Select(s => new CombatSkill { SkillId = s.SkillId!.Value, Name = s.Name, Value = s.Value })],
            FightBack = attacks.Where(a => a.Kind is CombatAttackKind.Melee or CombatAttackKind.Maneuver)
                .Select(a => a.Skill).DefaultIfEmpty(0).Max(),
        };
    }

    private static bool IsCode(string? code, string root) =>
        code is not null && (code == root || code.StartsWith(root + ".", StringComparison.Ordinal));
}
