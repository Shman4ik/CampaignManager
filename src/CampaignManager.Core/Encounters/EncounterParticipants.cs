using CampaignManager.Core.Catalogs;
using CampaignManager.Core.Characters;

namespace CampaignManager.Core.Encounters;

/// <summary>
/// Снимок участника из источника: лист (сыщик, НПС) или статблок твари. Числа берутся правилами листа
/// (<see cref="DerivedAttributeRules"/>, <see cref="SanityRules"/>), а не считаются здесь второй раз.
/// </summary>
public static class EncounterParticipants
{
    /// <summary>Участник из листа. Сторона — по виду: сыщик — с сыщиками; НПС — как скажут (из роли в сценарии).</summary>
    public static EncounterParticipant FromSheet(Guid characterId, CharacterKind kind, CharacterSheet sheet, SkillCatalog catalog,
        EncounterSide? side = null)
    {
        var name = string.IsNullOrWhiteSpace(sheet.Personal.Name) ? "Без имени" : sheet.Personal.Name.Trim();
        var participant = new EncounterParticipant
        {
            Kind = kind == CharacterKind.Player ? ParticipantKind.Investigator : ParticipantKind.Npc,
            SourceCharacterId = characterId,
            Name = name,
            SourceName = name,
            Side = side ?? (kind == CharacterKind.Player ? EncounterSide.Investigators : EncounterSide.Neutral),
            Initiative = sheet.Characteristics.Dex,
        };
        Refresh(participant, sheet, catalog);
        return participant;
    }

    /// <summary>
    /// Участник из статблока твари. Инициатива 0 в книге — «по ЛВК» (знание v1); рассудка и Удачи у твари нет.
    /// </summary>
    public static EncounterParticipant FromStatblock(Guid? creatureId, string name, Statblock statblock, EncounterSide side = EncounterSide.Enemies)
    {
        var title = string.IsNullOrWhiteSpace(name) ? "Тварь" : name.Trim();
        return new EncounterParticipant
        {
            Kind = ParticipantKind.Creature,
            SourceCreatureId = creatureId,
            Name = title,
            SourceName = title,
            Side = side,
            Initiative = statblock.Initiative > 0 ? statblock.Initiative : statblock.Dex.Value,
            Stats = new ParticipantStats
            {
                Str = statblock.Str.Value,
                Con = statblock.Con.Value,
                Siz = statblock.Siz.Value,
                Dex = statblock.Dex.Value,
                Int = statblock.Int.Value,
                Pow = statblock.Pow.Value,
                Build = statblock.Build,
                DamageBonus = string.IsNullOrWhiteSpace(statblock.DamageBonus) ? "0" : statblock.DamageBonus,
                Move = statblock.Speed.Move,
                Dodge = statblock.Dodge,
                Armor = statblock.Armor,
            },
            HitPoints = statblock.HitPoints,
            MaxHitPoints = statblock.HitPoints,
            MagicPoints = statblock.MagicPoints,
            MaxMagicPoints = statblock.MagicPoints,
            SanityLoss = string.IsNullOrWhiteSpace(statblock.SanityLoss) ? null : statblock.SanityLoss,
            Profile = CombatProfiles.FromStatblock(statblock),
        };
    }

    /// <summary>
    /// Числа участника из его листа: после записи эффекта в лист (истина — лист: там привыкание и поправки из книги) и
    /// при открытии сцены (лист мог поправить игрок). Имя в сцене, сторона и инициатива не трогаются — это решения
    /// Хранителя в сцене.
    /// </summary>
    public static void Refresh(EncounterParticipant participant, CharacterSheet sheet, SkillCatalog catalog)
    {
        var derived = DerivedAttributeRules.Compute(sheet, catalog);
        var c = sheet.Characteristics;
        participant.SourceName = string.IsNullOrWhiteSpace(sheet.Personal.Name) ? participant.SourceName : sheet.Personal.Name.Trim();
        participant.Stats = new ParticipantStats
        {
            Str = c.Str,
            Con = c.Con,
            Siz = c.Siz,
            Dex = c.Dex,
            Int = c.Int,
            Pow = c.Pow,
            Build = derived.Build,
            DamageBonus = derived.DamageBonus,
            Move = derived.Move,
            Dodge = derived.Dodge,
            Armor = participant.Stats.Armor,
            Extra = participant.Stats.Extra,
        };
        participant.HitPoints = sheet.Current.HitPoints;
        participant.MaxHitPoints = derived.MaxHitPoints;
        participant.MagicPoints = sheet.Current.MagicPoints;
        participant.MaxMagicPoints = derived.MaxMagicPoints;
        participant.Sanity = sheet.Current.Sanity;
        participant.MaxSanity = derived.MaxSanity;
        participant.Luck = sheet.Current.Luck;
        participant.MajorWound = sheet.Condition.MajorWound;
        participant.Unconscious = sheet.Condition.Unconscious;
        participant.Dying = sheet.Condition.Dying;
        participant.Stabilized = sheet.Condition.Stabilized;
        participant.Dead = sheet.Condition.Dead;
        participant.Profile = CombatProfiles.FromSheet(sheet, catalog);
    }

    /// <summary>Раны участника для <see cref="WoundRules"/> — те же поля, что у листа.</summary>
    public static WoundStatus Wounds(EncounterParticipant participant) => new(
        participant.HitPoints, participant.MajorWound, participant.Unconscious, participant.Dying, participant.Stabilized, participant.Dead);

    public static void SetWounds(EncounterParticipant participant, WoundStatus status)
    {
        participant.HitPoints = status.HitPoints;
        participant.MajorWound = status.MajorWound;
        participant.Unconscious = status.Unconscious;
        participant.Dying = status.Dying;
        participant.Stabilized = status.Stabilized;
        participant.Dead = status.Dead;
    }

    /// <summary>Сторона НПС по его роли в сценарии (знание v1): союзник — с сыщиками, враг — против, иначе нейтрален.</summary>
    public static EncounterSide SideOf(Scenarios.NpcRole role) => role switch
    {
        Scenarios.NpcRole.Ally => EncounterSide.Investigators,
        Scenarios.NpcRole.Enemy => EncounterSide.Enemies,
        _ => EncounterSide.Neutral,
    };
}
