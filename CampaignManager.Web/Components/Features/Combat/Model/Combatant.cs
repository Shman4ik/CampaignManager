using CampaignManager.Web.Components.Features.Bestiary.Model;
using CampaignManager.Web.Components.Features.Characters.Model;

namespace CampaignManager.Web.Components.Features.Combat.Model;

public class Combatant
{
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

    public bool IsPlayer { get; set; }

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

    public Combatant() { }

    public Combatant(Character character)
    {
        Id = character.Id;
        Name = character.PersonalInfo.Name;
        Dexterity = character.Characteristics.Dexterity.Regular;
        Initiative = character.Characteristics.Dexterity.Regular;

        MaxHitPoints = character.DerivedAttributes.HitPoints.MaxValue;
        CurrentHitPoints = character.DerivedAttributes.HitPoints.Value;

        MaxMagicPoints = character.DerivedAttributes.MagicPoints.MaxValue;
        CurrentMagicPoints = character.DerivedAttributes.MagicPoints.Value;

        MaxSanity = character.DerivedAttributes.Sanity.MaxValue;
        CurrentSanity = character.DerivedAttributes.Sanity.Value;

        IsPlayer = character.CharacterType != CharacterType.NonPlayerCharacter;
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

    public Combatant(Creature creature)
    {
        Id = Guid.NewGuid();
        Name = creature.Name;
        Dexterity = creature.CreatureCharacteristics.Dexterity.Value;
        Initiative = creature.CreatureCharacteristics.Initiative;

        MaxHitPoints = creature.CreatureCharacteristics.HealPoint;
        CurrentHitPoints = creature.CreatureCharacteristics.HealPoint;

        MaxMagicPoints = creature.CreatureCharacteristics.ManaPoint;
        CurrentMagicPoints = creature.CreatureCharacteristics.ManaPoint;

        MaxSanity = creature.CreatureCharacteristics.Power.Value;
        CurrentSanity = creature.CreatureCharacteristics.Power.Value;

        IsPlayer = false;
        CreatureSource = creature;

        // Боевые характеристики существа
        DamageBonus = creature.CreatureCharacteristics.AverageBonusToHit;
        ConstitutionValue = creature.CreatureCharacteristics.Constitution.Value;
        IntelligenceValue = creature.CreatureCharacteristics.Intelligence.Value;
        Luck = creature.CreatureCharacteristics.Luck;
        Build = creature.CreatureCharacteristics.AverageComplexity;
        Armor = creature.CreatureCharacteristics.Armor;

        DodgeSkill = creature.CreatureCharacteristics.DodgeSkill > 0
            ? creature.CreatureCharacteristics.DodgeSkill
            : creature.CreatureCharacteristics.Dexterity.Value / 2;

        FightingSkill = creature.Attacks
            .FirstOrDefault(a => a.IsMelee)?.SkillValue ?? 50;

        AttacksPerRound = creature.Attacks.Count > 0
            ? creature.Attacks.Max(a => a.AttacksPerRound)
            : 1;
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
