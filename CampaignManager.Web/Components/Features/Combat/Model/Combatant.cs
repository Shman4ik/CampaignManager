using CampaignManager.Web.Components.Features.Bestiary.Model;
using CampaignManager.Web.Components.Features.Characters.Model;

namespace CampaignManager.Web.Components.Features.Combat.Model;

public class Combatant
{
    /// <summary>
    ///     Идентификатор именно этого участника боя, а не листа персонажа: двух громил из
    ///     одного листа надо различать, иначе захват и удаление путают их между собой.
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public int Dexterity { get; set; }
    public int Initiative { get; set; }

    public int MaxHitPoints { get; set; }
    public int CurrentHitPoints { get; set; }

    public int MaxMagicPoints { get; set; }
    public int CurrentMagicPoints { get; set; }

    public int MaxSanity { get; set; }
    public int CurrentSanity { get; set; }

    /// <summary>
    ///     Сторона в бою. Союзный НПС стоит рядом с отрядом, а не среди монстров.
    /// </summary>
    public CombatSide Side { get; set; } = CombatSide.Enemy;

    // Состояния
    public bool IsUnconscious { get; set; }
    public bool HasMajorWound { get; set; }
    public bool IsDying { get; set; }

    /// <summary>
    /// Умирающего стабилизировали успешной Первой помощью: он получил 1 временный ПЗ,
    /// проверки ВЫН делаются раз в час, а не каждый раунд. Отметку «При смерти»
    /// снимает только последующая Медицина (стр. 118).
    /// </summary>
    public bool IsStabilized { get; set; }

    /// <summary>Временные ПЗ от Первой помощи умирающему (стр. 118).</summary>
    public int TemporaryHitPoints { get; set; }

    /// <summary>
    /// Первую помощь по текущему ранению уже пытались оказать. Повторная проверка
    /// правилами не допускается; сбрасывается при получении нового урона (стр. 118).
    /// </summary>
    public bool FirstAidAttempted { get; set; }
    public bool IsDead { get; set; }
    public bool HasTemporaryInsanity { get; set; }
    public bool HasIndefiniteInsanity { get; set; }

    // Боевые характеристики
    public int DodgeSkill { get; set; }
    public int FightingSkill { get; set; }

    /// <summary>ИНТ — нужен для проверки при потере 5+ пунктов рассудка (стр. 153).</summary>
    public int IntelligenceValue { get; set; }

    /// <summary>
    /// Удача. При крахе стрельбы в ближнем бою пулю получает союзник с наименьшей
    /// Удачей (стр. 112).
    /// </summary>
    public int Luck { get; set; }

    /// <summary>
    /// Потеряно рассудка за текущий игровой день. Потеря не менее ⅕ текущего
    /// рассудка за день означает бессрочное безумие (стр. 153).
    /// </summary>
    public int SanityLostToday { get; set; }

    /// <summary>Часов, оставшихся до конца временного безумия (1d10 при наступлении).</summary>
    public int TemporaryInsanityHours { get; set; }

    /// <summary>Неизлечимое безумие: рассудок упал до нуля (стр. 153).</summary>
    public bool HasPermanentInsanity { get; set; }
    public string DamageBonus { get; set; } = "0";
    public int Build { get; set; }
    public int ConstitutionValue { get; set; }

    /// <summary>
    /// Броня: вычитается из физического урона, но не снижает урон от магии,
    /// яда и утопления (стр. 106).
    /// </summary>
    public int Armor { get; set; }

    // Тактические состояния
    public bool IsProne { get; set; }      // Повалён
    public bool IsGrappled { get; set; }   // В захвате
    public Guid? GrappledBy { get; set; }  // Кто держит

    /// <summary>Оружие выбито удачным манёвром «Разоружить» (стр. 103).</summary>
    public bool IsDisarmed { get; set; }

    /// <summary>
    /// Поставлен в невыгодное положение манёвром. Правила не задают точный эффект —
    /// это отметка для Хранителя, который сам решает, какую кость выдать (стр. 103).
    /// </summary>
    public bool HasDisadvantage { get; set; }

    /// <summary>
    /// Сколько Удачи уже потрачено, чтобы не потерять сознание. Цена удваивается
    /// каждый раунд: 1, 2, 4, 8… Необязательное правило (стр. 123).
    /// </summary>
    public int LuckSpentToStayConscious { get; set; }

    // Трекинг раунда
    public bool HasFirearmReady { get; set; }     // Огнестрельное на изготовку (+50 к ЛВК)

    // ── Броски на инициативу (необязательное правило, стр. 122) ──────────

    /// <summary>Результат проверки ЛВК на инициативу.</summary>
    public int? InitiativeRoll { get; set; }

    /// <summary>Кости этой проверки — огнестрельное на изготовку даёт бонусную.</summary>
    public DiceRollResult? InitiativeRollDetail { get; set; }

    /// <summary>Уровень успеха проверки ЛВК; по нему строится очерёдность.</summary>
    public int InitiativeRollLevel { get; set; }

    /// <summary>Выпало 01 — тактическое преимущество или бонусная кость к первой атаке.</summary>
    public bool HasTacticalAdvantage { get; set; }

    /// <summary>Крах в проверке ЛВК — боец пропускает ход.</summary>
    public bool SkipsTurnFromFumble { get; set; }
    public bool HasDefendedThisRound { get; set; } // Уже защищался в этом раунде
    public int DefenseCountThisRound { get; set; } // Число защитных действий за раунд
    public int AttacksPerRound { get; set; } = 1;  // Число атак за раунд (существа)
    public bool IsAiming { get; set; }             // Прицеливается (бонусная кость в след. раунде)
    public bool HasTakenCover { get; set; }        // Укрылся от огня

    /// <summary>
    /// Номер раунда, в котором боец не может атаковать: укрытие от огня отнимает
    /// следующую атаку — текущего раунда, если он ещё не атаковал, иначе следующего
    /// (стр. 111). У существ с несколькими атаками пропадают все атаки этого раунда.
    /// </summary>
    public int? AttackBlockedInRound { get; set; }

    /// <summary>Сколько атак боец уже совершил в этом раунде (стр. 100).</summary>
    public int AttacksThisRound { get; set; }
    public bool HasActedThisRound { get; set; }    // Уже действовал в этом раунде
    public bool IsDelayed { get; set; }            // Отложил действие
    public string? JammedWeaponName { get; set; }  // Заклинившее оружие (название)

    /// <summary>Сколько боевых раундов ещё займёт починка заклинившего оружия (1d6, стр. 113).</summary>
    public int JamRepairRoundsLeft { get; set; }

    /// <summary>
    /// Сколько проверок атаки автоматическим оружием уже сделано в этом раунде.
    /// Каждая следующая получает штрафную кость (стр. 114). Сбрасывается каждый раунд.
    /// </summary>
    public int AutofireChecksThisRound { get; set; }

    /// <summary>Патронов в оружии, по названию оружия (стр. 111).</summary>
    public Dictionary<string, int> AmmoLoaded { get; set; } = [];

    // Ссылки на исходные сущности
    public Character? CharacterSource { get; set; }
    public Creature? CreatureSource { get; set; }

    /// <summary>Лист персонажа, с которого снят участник (для переноса урона в лист).</summary>
    public Guid? SourceCharacterId { get; set; }

    /// <summary>Существо бестиария или сценария, с которого снят участник.</summary>
    public Guid? SourceCreatureId { get; set; }

    public Combatant() { }

    public Combatant(Character character, CombatSide side = CombatSide.Party, string? nameSuffix = null)
    {
        Name = string.IsNullOrEmpty(nameSuffix)
            ? character.PersonalInfo.Name
            : $"{character.PersonalInfo.Name} {nameSuffix}";
        SourceCharacterId = character.Id;
        Side = side;
        Dexterity = character.Characteristics.Dexterity.Regular;
        Initiative = character.Characteristics.Dexterity.Regular;

        MaxHitPoints = character.DerivedAttributes.HitPoints.MaxValue;
        CurrentHitPoints = character.DerivedAttributes.HitPoints.Value;

        MaxMagicPoints = character.DerivedAttributes.MagicPoints.MaxValue;
        CurrentMagicPoints = character.DerivedAttributes.MagicPoints.Value;

        MaxSanity = character.DerivedAttributes.Sanity.MaxValue;
        CurrentSanity = character.DerivedAttributes.Sanity.Value;

        CharacterSource = character;

        // Боевые характеристики
        DodgeSkill = character.PersonalInfo.Dodge;
        FightingSkill = FindFightingSkill(character);
        DamageBonus = character.PersonalInfo.DamageBonus;
        _ = int.TryParse(character.PersonalInfo.Build, out var buildValue);
        Build = buildValue;
        ConstitutionValue = character.Characteristics.Constitution.Regular;
        IntelligenceValue = character.Characteristics.Intelligence.Regular;
        Luck = character.DerivedAttributes.Luck.Value;

        // Состояния из персонажа
        if (character.State != null)
        {
            IsUnconscious = character.State.IsUnconscious;
            HasMajorWound = character.State.HasSeriousInjury;
            IsDying = character.State.IsDying;
            HasTemporaryInsanity = character.State.HasTemporaryInsanity;
            HasIndefiniteInsanity = character.State.HasIndefiniteInsanity;
        }
    }

    public Combatant(Creature creature, CombatSide side = CombatSide.Enemy, string? nameSuffix = null)
    {
        Name = string.IsNullOrEmpty(nameSuffix) ? creature.Name : $"{creature.Name} {nameSuffix}";
        SourceCreatureId = creature.Id;
        Side = side;
        Dexterity = creature.CreatureCharacteristics.Dexterity.Value;
        // Очерёдность идёт по убыванию ЛВК (стр. 110). Собственная инициатива —
        // необязательная правка Хранителя, ноль означает «взять ЛВК»; без этого
        // запаса любая тварь ходила бы после любого сыщика.
        Initiative = creature.CreatureCharacteristics.Initiative > 0
            ? creature.CreatureCharacteristics.Initiative
            : creature.CreatureCharacteristics.Dexterity.Value;

        MaxHitPoints = creature.CreatureCharacteristics.HealPoint;
        CurrentHitPoints = creature.CreatureCharacteristics.HealPoint;

        MaxMagicPoints = creature.CreatureCharacteristics.ManaPoint;
        CurrentMagicPoints = creature.CreatureCharacteristics.ManaPoint;

        MaxSanity = creature.CreatureCharacteristics.Power.Value;
        CurrentSanity = creature.CreatureCharacteristics.Power.Value;

        CreatureSource = creature;

        // Боевые характеристики существа
        DamageBonus = creature.CreatureCharacteristics.AverageDamageBonus;
        ConstitutionValue = creature.CreatureCharacteristics.Constitution.Value;
        IntelligenceValue = creature.CreatureCharacteristics.Intelligence.Value;
        // Удачи у чудовищ книга не указывает: правило шальной пули (стр. 112) ищет
        // невезучего среди союзников-сыщиков, а не среди тварей.
        Build = creature.CreatureCharacteristics.AverageComplexity;
        Armor = creature.CreatureCharacteristics.Armor;

        DodgeSkill = creature.CreatureCharacteristics.DodgeSkill > 0
            ? creature.CreatureCharacteristics.DodgeSkill
            : creature.CreatureCharacteristics.Dexterity.Value / 2;

        // Базовый боевой навык существа — именно строка «Ближний бой» (стр. 278);
        // ей же оно совершает манёвры и контратакует. Брать первую попавшуюся
        // строку нельзя: у твари могут быть укус и захват со своими процентами.
        FightingSkill = FindMeleeSkill(creature);

        // Атак за раунд — строка статблока, одна на всё существо (стр. 279).
        AttacksPerRound = Math.Max(1, creature.CreatureCharacteristics.AttacksPerRound);
    }

    /// <summary>
    ///     Процент базовой атаки существа: строка «Ближний бой», иначе самая частая
    ///     атака ближнего боя, иначе стандартные 50%.
    /// </summary>
    private static int FindMeleeSkill(Creature creature)
    {
        var melee = creature.Attacks
            .FirstOrDefault(a => a.Name.StartsWith("Ближний бой", StringComparison.OrdinalIgnoreCase));

        return melee?.SkillValue
               ?? creature.Attacks.FirstOrDefault(a => a.Kind == CreatureAttackKind.Melee)?.SkillValue
               ?? 50;
    }

    private static int FindFightingSkill(Character character)
    {
        if (character.Skills?.SkillGroups == null) return 25;

        foreach (var group in character.Skills.SkillGroups)
        {
            foreach (var skill in group.Skills)
            {
                if (skill.Name.Contains("Ближний бой", StringComparison.OrdinalIgnoreCase)
                    || skill.Name.Contains("драка", StringComparison.OrdinalIgnoreCase))
                {
                    return skill.Value.Regular;
                }
            }
        }

        return 25; // базовое значение
    }
}
