using CampaignManager.Web.Components.Features.Bestiary.Model;
using CampaignManager.Web.Components.Features.Combat.Model;
using CampaignManager.Web.Components.Features.Combat.Services;
using CampaignManager.Web.Components.Features.Weapons.Model;
using CampaignManager.Web.Components.Features.Weapons.Services;

namespace CampaignManager.Web.Components.Features.Combat.Components.Attack;

/// <summary>Поле урона, которое Хранитель вписывает или бросает в панели атаки.</summary>
public enum AttackDamageField { Weapon, DamageBonus, ExtraImpaling, CounterWeapon, CounterBonus }

/// <summary>Строка списка «Оружие / Атака»: оружие сыщика, атака существа или безоружная атака.</summary>
public sealed class AttackOption
{
    public string Name { get; set; } = "";
    public string Damage { get; set; } = "";
    public Weapon? Weapon { get; set; }
    public bool IsMelee { get; set; }
    public int SkillValue { get; set; }

    /// <summary>Режим бонуса к урону из статблока; пусто для оружия сыщика.</summary>
    public CreatureDamageBonusMode? CreatureDamageBonus { get; set; }
}

/// <summary>
///     Всё, что Хранитель набрал в панели атаки (<c>AttackSetupPanel</c>): кто бьёт, чем, по кому,
///     броски, кости и урон. Один экземпляр на панель, общий для её частей — дальнего боя
///     (<c>AttackRangedOptions</c>), защиты в ближнем бою (<c>AttackDefenseOptions</c>), бросков
///     (<c>AttackRollInputs</c>), модификаторов (<c>AttackModifierOptions</c>) и урона
///     (<c>AttackDamageInputs</c>).
///     <para>
///         Здесь только состояние панели и то, что из него следует для экрана. Правила — в
///         <see cref="CombatService" />: модификаторы и уровень успеха считаются им же и при
///         предпросмотре, и при разрешении атаки, чтобы они не расходились. Методы, которым
///         нужен сам бой (починка, перезарядка, разрешение), остаются в компонентах.
///     </para>
/// </summary>
public sealed class AttackSetupState
{
    public string SelectedAttackerId { get; private set; } = "";
    public string SelectedDefenderId { get; private set; } = "";
    public int SelectedWeaponIndex { get; private set; } = -1;
    public Combatant? SelectedAttacker { get; private set; }
    public Combatant? SelectedDefender { get; private set; }
    public int AttackSkillValue { get; set; }
    public int DefenderSkillValue { get; set; }
    public bool IsMelee { get; private set; }
    public CombatActionType DefenderReaction { get; private set; } = CombatActionType.Dodge;
    public RangeLevel RangeLevel { get; private set; } = RangeLevel.Base;
    public string CounterDamageFormula { get; set; } = "1D3";

    // Броски кубиков
    public int? AttackerRoll { get; private set; }
    public int? DefenderRoll { get; private set; }
    public DiceRollResult? AttackerRollDetail { get; private set; }
    public DiceRollResult? DefenderRollDetail { get; private set; }
    public int? WeaponDamageRoll { get; private set; }
    public int? DamageBonusRoll { get; private set; }
    public int? ExtraImpalingRoll { get; private set; }
    public int? CounterWeaponRoll { get; private set; }
    public int? CounterBonusRoll { get; private set; }

    // Уровни успеха (вычисляются в реальном времени)
    public SuccessLevel AttackerSuccessLevel { get; private set; }
    public SuccessLevel DefenderSuccessLevel { get; private set; }

    // Модификаторы
    public int BonusDice { get; set; }
    public int PenaltyDice { get; set; }
    public int DefenderBonusDice { get; set; }
    public int DefenderPenaltyDice { get; set; }
    public SurpriseMode Surprise { get; private set; } = SurpriseMode.TargetReady;

    // Модификаторы стрельбы (CoC 7e стр. 110–113)
    public bool IsPointBlank { get; set; }
    public bool IsAiming { get; set; }
    public bool IsFiringIntoMelee { get; set; }
    public bool IsTargetTakingCover { get; set; }
    public bool IsTargetBehindCover { get; set; }
    public bool IsTargetFastMoving { get; set; }
    public FiringMode FiringMode { get; private set; } = FiringMode.Single;
    public int ShotsInVolley { get; set; } = 3;
    public bool IsReloadAndFire { get; set; }
    public bool IgnoresArmor { get; set; }
    public int CoverArmor { get; set; }

    // Ручной ввод ВЫН
    private int? _manualConRoll;
    private int? _manualCounterConRoll;

    // Починка заклинившего оружия (стр. 113)
    public int RepairSkill { get; set; } = 30;
    public int? RepairRoll { get; set; }
    public string? RepairMessage { get; set; }

    public List<AttackOption> AttackOptions { get; } = [];

    /// <summary>Выбранная строка «Оружие / Атака», если она есть.</summary>
    public AttackOption? SelectedOption =>
        SelectedWeaponIndex >= 0 && SelectedWeaponIndex < AttackOptions.Count
            ? AttackOptions[SelectedWeaponIndex]
            : null;

    /// <summary>
    /// Режим с учётом правила: автоматическое попадание недоступно для дистанционных атак.
    /// </summary>
    public SurpriseMode EffectiveSurprise =>
        !IsMelee && Surprise == SurpriseMode.AutoHit ? SurpriseMode.BonusDie : Surprise;

    // ───────────────────── Выбор участников и оружия ─────────────────────

    public void SelectAttacker(Combatant? attacker)
    {
        SelectedAttacker = attacker;
        SelectedAttackerId = attacker?.Id.ToString() ?? "";
        SelectedWeaponIndex = -1;
        AttackOptions.Clear();
        ResetRolls();

        if (SelectedAttacker == null) return;

        if (SelectedAttacker.CharacterSource != null)
        {
            foreach (var w in SelectedAttacker.CharacterSource.Weapons)
            {
                AttackOptions.Add(new AttackOption
                {
                    Name = w.Name,
                    Damage = w.Damage,
                    Weapon = w,
                    IsMelee = w.Type == WeaponType.Melee,
                    SkillValue = CombatService.FindSkillValue(SelectedAttacker.CharacterSource, w)
                });
            }
        }
        else if (SelectedAttacker.CreatureSource != null)
        {
            foreach (var attack in SelectedAttacker.CreatureSource.Attacks)
            {
                AttackOptions.Add(new AttackOption
                {
                    Name = attack.Kind is CreatureAttackKind.Maneuver
                        ? $"{attack.Name} (манёвр)"
                        : attack.Name,
                    Damage = attack.DamageFormula,
                    IsMelee = attack.IsMelee,
                    SkillValue = attack.SkillValue,
                    CreatureDamageBonus = attack.DamageBonus
                });
            }
        }

        AttackOptions.Add(new AttackOption
        {
            Name = "Безоружная атака",
            Damage = "1D3",
            IsMelee = true,
            SkillValue = SelectedAttacker.FightingSkill
        });
    }

    public void SelectWeapon(int index)
    {
        SelectedWeaponIndex = index;
        ResetRolls();
        if (SelectedWeaponIndex >= 0 && SelectedWeaponIndex < AttackOptions.Count)
        {
            var option = AttackOptions[SelectedWeaponIndex];
            IsMelee = option.IsMelee;
            AttackSkillValue = option.SkillValue;
            RangeLevel = RangeLevel.Base;
        }

        ClampFiringModeToWeapon();
    }

    public void SelectDefender(string defenderId, Combatant? defender)
    {
        SelectedDefenderId = defenderId;
        SelectedDefender = defender;
        ResetRolls();
        UpdateDefenderSkill();
        UpdateCounterFormula();
    }

    public void SetDefenderReaction(CombatActionType reaction)
    {
        DefenderReaction = reaction;
        ResetRolls();
        UpdateDefenderSkill();
    }

    public void SetRangeLevel(RangeLevel range)
    {
        RangeLevel = range;
        ResetRolls();
    }

    public void SetSurprise(SurpriseMode mode)
    {
        Surprise = mode;
        ResetRolls();
    }

    public void SetFiringMode(FiringMode mode)
    {
        FiringMode = mode;
        if (mode == FiringMode.Volley && SelectedAttacker != null)
            ShotsInVolley = CombatService.GetVolleySize(AttackSkillValue);
        ResetRolls();
    }

    private void UpdateDefenderSkill()
    {
        if (SelectedDefender == null) return;
        DefenderSkillValue = DefenderReaction == CombatActionType.Dodge
            ? SelectedDefender.DodgeSkill
            : SelectedDefender.FightingSkill;
    }

    private void UpdateCounterFormula()
    {
        if (SelectedDefender?.CharacterSource?.Weapons.Count > 0)
            CounterDamageFormula = SelectedDefender.CharacterSource.Weapons[0].Damage;
        else
            CounterDamageFormula = "1D3";
    }

    /// <summary>
    /// Режим огня, недоступный выбранному оружию, сбрасывается на одиночный выстрел:
    /// иначе после смены ствола панель осталась бы в режиме, которого у него нет.
    /// </summary>
    private void ClampFiringModeToWeapon()
    {
        if (FiringMode == FiringMode.PistolBurst && !AllowsMultipleShots) FiringMode = FiringMode.Single;
        if (FiringMode == FiringMode.Volley && !AllowsVolley) FiringMode = FiringMode.Single;
    }

    public void ResetRolls()
    {
        AttackerRoll = null;
        DefenderRoll = null;
        AttackerRollDetail = null;
        DefenderRollDetail = null;
        DefenderBonusDice = 0;
        DefenderPenaltyDice = 0;
        WeaponDamageRoll = null;
        DamageBonusRoll = null;
        ExtraImpalingRoll = null;
        CounterWeaponRoll = null;
        CounterBonusRoll = null;
        _manualConRoll = null;
        _manualCounterConRoll = null;
        AttackerSuccessLevel = SuccessLevel.Failure;
        DefenderSuccessLevel = SuccessLevel.Failure;
        IsPointBlank = false;
        IsAiming = false;
        IsFiringIntoMelee = false;
        IsTargetTakingCover = false;
        IsTargetBehindCover = false;
        IsTargetFastMoving = false;
        IsReloadAndFire = false;
        IgnoresArmor = false;
        CoverArmor = 0;
    }

    // ───────────────────── Броски ─────────────────────

    /// <summary>
    /// Хранитель ввёл итог своих физических костей — модификаторы он применил сам, поэтому
    /// расшифровки костей у такого броска нет. Уровень атаки считается со сложностью
    /// <see cref="RequiredLevel" />, как в <see cref="CombatService.ResolveRangedAttack" />: у стрельбы
    /// на большой дальности крах наступает с 96, если половина навыка меньше 50 (стр. 88).
    /// </summary>
    public void EnterRoll(string? raw, bool isAttacker)
    {
        if (!int.TryParse(raw, out var v)) return;
        v = Math.Clamp(v, 1, 100);

        if (isAttacker)
        {
            AttackerRoll = v;
            AttackerRollDetail = null;
            AttackerSuccessLevel = CombatService.CalculateSuccessLevel(v, AttackSkillValue, RequiredLevel);
        }
        else
        {
            DefenderRoll = v;
            DefenderRollDetail = null;
            DefenderSuccessLevel = CombatService.CalculateSuccessLevel(v, DefenderSkillValue);
        }
    }

    public void AutoRollAttacker()
    {
        var mods = CurrentModifiers();
        AttackerRollDetail = CombatService.RollD100(mods.BonusDice, mods.PenaltyDice);
        AttackerRoll = AttackerRollDetail.Result;
        AttackerSuccessLevel = CombatService.CalculateSuccessLevel(AttackerRoll.Value, AttackSkillValue, RequiredLevel);
    }

    public void AutoRollDefender()
    {
        DefenderRollDetail = CombatService.RollD100(DefenderBonusDice, DefenderPenaltyDice);
        DefenderRoll = DefenderRollDetail.Result;
        DefenderSuccessLevel = CombatService.CalculateSuccessLevel(DefenderRoll.Value, DefenderSkillValue);
    }

    public void EnterDamage(string? raw, AttackDamageField field)
    {
        if (!int.TryParse(raw, out var v)) return;
        SetDamage(field, v);
    }

    public void AutoRollDamage(AttackDamageField field, string formula) =>
        SetDamage(field, CombatService.RollDiceFormula(formula));

    private void SetDamage(AttackDamageField field, int value)
    {
        switch (field)
        {
            case AttackDamageField.Weapon: WeaponDamageRoll = value; break;
            case AttackDamageField.DamageBonus: DamageBonusRoll = value; break;
            case AttackDamageField.ExtraImpaling: ExtraImpalingRoll = value; break;
            case AttackDamageField.CounterWeapon: CounterWeaponRoll = value; break;
            case AttackDamageField.CounterBonus: CounterBonusRoll = value; break;
        }
    }

    // ───────────────────── Что следует из выбора ─────────────────────

    /// <summary>
    /// Уровень успеха, нужный для попадания: в ближнем бою обычный,
    /// в дальнем — заданный дальностью (CoC 7e, стр. 110).
    /// </summary>
    public SuccessLevel RequiredLevel
    {
        get
        {
            if (IsMelee) return SuccessLevel.RegularSuccess;

            var byRange = CombatService.GetRequiredLevelForRange(RangeLevel);
            var index = FiringMode == FiringMode.Volley ? SelectedAttacker?.AutofireChecksThisRound ?? 0 : 0;
            return CombatService.EscalateAutofire(byRange, index).Required;
        }
    }

    /// <summary>Сложность выросла выше критической — попадание невозможно (стр. 114).</summary>
    public bool ShotIsImpossible =>
        !IsMelee
        && FiringMode == FiringMode.Volley
        && CombatService.EscalateAutofire(
               CombatService.GetRequiredLevelForRange(RangeLevel),
               SelectedAttacker?.AutofireChecksThisRound ?? 0).IsImpossible;

    /// <summary>
    /// Попадает ли атака: при автоматическом попадании промахом считается только крах,
    /// иначе нужен уровень успеха, требуемый дальностью.
    /// </summary>
    public bool AttackConnects =>
        EffectiveSurprise == SurpriseMode.AutoHit
            ? AttackerSuccessLevel != SuccessLevel.Fumble
            : AttackerSuccessLevel >= RequiredLevel;

    /// <summary>Выбранное оружие, если оно есть.</summary>
    public Weapon? SelectedWeapon => SelectedOption?.Weapon;

    /// <summary>Ёмкость магазина выбранного оружия, 0 — если её нет в данных.</summary>
    public int AmmoCapacity => WeaponStatsReader.AmmoCapacity(SelectedWeapon) ?? 0;

    /// <summary>Базовая дальность выбранного оружия в метрах; null — её нет в данных.</summary>
    public int? BaseRangeMeters => WeaponStatsReader.BaseRangeMeters(SelectedWeapon);

    /// <summary>Разобранное число атак выбранного оружия.</summary>
    private WeaponAttacksInfo SelectedAttacks => WeaponStatsReader.Attacks(SelectedWeapon);

    /// <summary>
    /// Предел выстрелов за раунд: «1 (3)» — три. Без разобранного значения предел не
    /// известен, и панель не ограничивает Хранителя.
    /// </summary>
    public int MaxShotsPerRound => WeaponStatsReader.MaxShotsPerRound(SelectedWeapon) ?? 1;

    /// <summary>Оружие умеет стрелять сериями: «1 (3)», «1 или 2».</summary>
    public bool AllowsMultipleShots => MaxShotsPerRound > 1;

    /// <summary>Оружие умеет вести непрерывный огонь или стрелять очередями.</summary>
    public bool AllowsVolley => SelectedAttacks is { AllowsFullAuto: true } or { AllowsBurst: true };

    /// <summary>Подпись режима залпа — очередь фиксированной длины или непрерывный огонь.</summary>
    public string VolleyModeLabel => SelectedAttacks switch
    {
        { AllowsFullAuto: true, AllowsBurst: true, BurstSize: { } size } =>
            $"Очередь по {size} или непрерывный огонь",
        { AllowsFullAuto: true } => "Непрерывный огонь",
        { AllowsBurst: true, BurstSize: { } size } => $"Очередь по {size}",
        _ => "Очередь или залп автоматического оружия"
    };

    /// <summary>
    /// Дальность в метрах для переключателя: базовая, вдвое и вчетверо больше (стр. 108).
    /// Пусто, если базовой дальности в данных оружия нет.
    /// </summary>
    public string RangeHint(int multiplier) =>
        BaseRangeMeters is { } meters ? $" — до {meters * multiplier} м" : string.Empty;

    /// <summary>
    /// Сколько патронов уходит на текущую проверку атаки. Серия и залп ограничены
    /// пределом выстрелов за раунд, если он известен.
    /// </summary>
    public int ShotsPerCheck => FiringMode == FiringMode.Volley ? Math.Max(1, ShotsInVolley) : 1;

    /// <summary>Выбранное оружие заклинило — стрелять из него нельзя (стр. 113).</summary>
    public bool WeaponIsJammed =>
        SelectedAttacker is not null
        && !string.IsNullOrEmpty(SelectedAttacker.JammedWeaponName)
        && SelectedWeaponIndex >= 0
        && AttackOptions[SelectedWeaponIndex].Name == SelectedAttacker.JammedWeaponName;

    /// <summary>Патронов в выбранном оружии атакующего.</summary>
    public int AmmoLeft => SelectedAttacker is null
        ? 0
        : CombatService.GetAmmoLeft(SelectedAttacker, AttackOptions[SelectedWeaponIndex].Weapon);

    /// <summary>
    /// Модификаторы текущей атаки. Считаются сервисом, чтобы предпросмотр
    /// и фактический бросок не расходились.
    /// </summary>
    public AttackModifiers CurrentModifiers() =>
        SelectedAttacker is null
            ? AttackModifiers.None
            : CombatService.CalculateAttackModifiers(BuildSetup(), SelectedAttacker, SelectedDefender);

    /// <summary>Побеждает ли атакующий во встречном броске; без броска защиты — да.</summary>
    public bool ResolveOpposed()
    {
        if (!DefenderRoll.HasValue) return true;
        return CombatService.ResolveOpposedRoll(
            AttackerSuccessLevel, AttackSkillValue,
            DefenderSuccessLevel, DefenderSkillValue,
            DefenderReaction);
    }

    /// <summary>
    /// Собирает настройку атаки из состояния панели. Используется и для предпросмотра
    /// модификаторов, и для фактического разрешения атаки.
    /// </summary>
    public AttackSetup BuildSetup()
    {
        var option = SelectedOption;

        return new AttackSetup
        {
            AttackerId = SelectedAttacker?.Id ?? Guid.Empty,
            DefenderId = SelectedDefender?.Id ?? Guid.Empty,
            SelectedWeapon = option?.Weapon,
            CreatureAttackName = option is { Weapon: null } ? option.Name : null,
            CreatureAttackDamage = option is { Weapon: null } ? option.Damage : null,
            CreatureDamageBonus = option is { Weapon: null } ? option.CreatureDamageBonus : null,
            AttackSkillValue = AttackSkillValue,
            IsMelee = IsMelee,
            DefenderReaction = DefenderReaction,
            DefenderSkillValue = DefenderSkillValue,
            RangeLevel = RangeLevel,
            BonusDice = BonusDice,
            PenaltyDice = PenaltyDice,
            DefenderBonusDice = DefenderBonusDice,
            DefenderPenaltyDice = DefenderPenaltyDice,
            SurpriseMode = Surprise,
            CounterAttackDamageFormula = CounterDamageFormula,
            ManualAttackerRoll = AttackerRoll,
            ManualDefenderRoll = DefenderRoll,
            AttackerRollDetail = AttackerRollDetail,
            DefenderRollDetail = DefenderRollDetail,
            ManualWeaponDamageRoll = WeaponDamageRoll,
            ManualDamageBonusRoll = DamageBonusRoll,
            ManualExtraImpalingRoll = ExtraImpalingRoll,
            ManualCounterWeaponDamageRoll = CounterWeaponRoll,
            ManualCounterDamageBonusRoll = CounterBonusRoll,
            ManualMajorWoundConRoll = _manualConRoll,
            ManualCounterMajorWoundConRoll = _manualCounterConRoll,
            IsPointBlank = IsPointBlank,
            IsAiming = IsAiming,
            IsFiringIntoMelee = IsFiringIntoMelee,
            IsTargetTakingCover = IsTargetTakingCover,
            IsTargetBehindCover = IsTargetBehindCover,
            IsTargetFastMoving = IsTargetFastMoving,
            FiringMode = FiringMode,
            AutofireCheckIndex = SelectedAttacker?.AutofireChecksThisRound ?? 0,
            ShotsFired = ShotsPerCheck,
            IsReloadAndFire = IsReloadAndFire,
            IgnoresArmor = IgnoresArmor,
            CoverArmor = CoverArmor
        };
    }
}
