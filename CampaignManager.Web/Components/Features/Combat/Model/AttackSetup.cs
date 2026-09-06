using CampaignManager.Web.Components.Features.Weapons.Model;

namespace CampaignManager.Web.Components.Features.Combat.Model;

/// <summary>
/// DTO для настройки атаки мастером перед разрешением
/// </summary>
public class AttackSetup
{
    public Guid AttackerId { get; set; }
    public Guid DefenderId { get; set; }

    /// <summary>Оружие персонажа (для игроков)</summary>
    public Weapon? SelectedWeapon { get; set; }

    /// <summary>Название атаки существа (из CombatDescriptions)</summary>
    public string? CreatureAttackName { get; set; }

    /// <summary>Урон атаки существа (свободный текст, напр. "1D6+1D4")</summary>
    public string? CreatureAttackDamage { get; set; }

    /// <summary>Навык атаки (базовое значение, до модификаторов дальности)</summary>
    public int AttackSkillValue { get; set; }

    /// <summary>Ближний бой или дальний</summary>
    public bool IsMelee { get; set; }

    /// <summary>Реакция защитника (Уклонение или Ответный удар), только для ближнего боя</summary>
    public CombatActionType DefenderReaction { get; set; } = CombatActionType.Dodge;

    /// <summary>Навык защитника (уклонение или ближний бой)</summary>
    public int DefenderSkillValue { get; set; }

    // ── Ручной ввод бросков (null = авторбросок) ────────────────────────

    /// <summary>Результат d100 атакующего (null = авторбросок)</summary>
    public int? ManualAttackerRoll { get; set; }

    /// <summary>Результат d100 защитника (null = авторбросок)</summary>
    public int? ManualDefenderRoll { get; set; }

    /// <summary>
    /// Уже брошенные кости атакующего (панель бросает их заранее, чтобы показать результат).
    /// Если задано, повторный бросок не выполняется.
    /// </summary>
    public DiceRollResult? AttackerRollDetail { get; set; }

    /// <summary>Уже брошенные кости защитника.</summary>
    public DiceRollResult? DefenderRollDetail { get; set; }

    /// <summary>Итог броска урона оружия (null = авторбросок). Не используется при чрезвычайном/крит. успехе.</summary>
    public int? ManualWeaponDamageRoll { get; set; }

    /// <summary>Итог броска бонуса к урону (null = авторбросок)</summary>
    public int? ManualDamageBonusRoll { get; set; }

    /// <summary>Дополнительный бросок урона при проникающей ране (null = авторбросок)</summary>
    public int? ManualExtraImpalingRoll { get; set; }

    /// <summary>Урон контратаки по атакующему — итог броска оружия (null = авторбросок)</summary>
    public int? ManualCounterWeaponDamageRoll { get; set; }

    /// <summary>Урон контратаки по атакующему — итог броска БкУ (null = авторбросок)</summary>
    public int? ManualCounterDamageBonusRoll { get; set; }

    /// <summary>Формула урона контратаки защитника (если fight back)</summary>
    public string CounterAttackDamageFormula { get; set; } = "1D3";

    // ── Модификаторы (бонусные / штрафные кости) ─────────────────────────

    /// <summary>
    /// Бонусные кости атакующего, назначенные Хранителем вручную.
    /// Автоматические кости по правилам добавляет <c>CombatService.CalculateAttackModifiers</c>.
    /// </summary>
    public int BonusDice { get; set; }

    /// <summary>Штрафные кости атакующего, назначенные Хранителем вручную.</summary>
    public int PenaltyDice { get; set; }

    /// <summary>Бонусные кости защитника (уклонение или контратака), назначаются вручную.</summary>
    public int DefenderBonusDice { get; set; }

    /// <summary>Штрафные кости защитника, назначаются вручную.</summary>
    public int DefenderPenaltyDice { get; set; }

    // ── Особые условия ───────────────────────────────────────────────────

    /// <summary>
    /// Готовность цели к атаке (стр. 104–105). Определяет, защищается ли цель
    /// и получает ли атакующий автоматическое попадание или бонусную кость.
    /// </summary>
    public SurpriseMode SurpriseMode { get; set; } = SurpriseMode.TargetReady;

    // ── Ручной ввод бросков ВЫН при серьёзной ране ────────────────────────

    /// <summary>Ручной бросок ВЫН защитника при серьёзной ране (null = авторбросок)</summary>
    public int? ManualMajorWoundConRoll { get; set; }

    /// <summary>Ручной бросок ВЫН атакующего при серьёзной ране от контратаки (null = авторбросок)</summary>
    public int? ManualCounterMajorWoundConRoll { get; set; }

    // ── Дальность (огнестрельное) ────────────────────────────────────────

    /// <summary>Дальность стрельбы — влияет на эффективное значение навыка</summary>
    public RangeLevel RangeLevel { get; set; } = RangeLevel.Base;

    // ── Модификаторы стрельбы (CoC 7e стр. 110–113) ─────────────────────

    /// <summary>Стрельба в упор (≤ ЛВК/15 метров) → бонусная кость</summary>
    public bool IsPointBlank { get; set; }

    /// <summary>Прицеливание (потрачен прошлый раунд) → бонусная кость</summary>
    public bool IsAiming { get; set; }

    /// <summary>Стрельба в ближнем бою → штрафная кость</summary>
    public bool IsFiringIntoMelee { get; set; }

    /// <summary>Цель укрылась от огня → штрафная кость</summary>
    public bool IsTargetTakingCover { get; set; }

    /// <summary>Частичное укрытие (≥50%) → штрафная кость</summary>
    public bool IsTargetBehindCover { get; set; }

    /// <summary>Быстро движущаяся цель (СКО ≥8) → штрафная кость</summary>
    public bool IsTargetFastMoving { get; set; }

    /// <summary>
    /// Режим стрельбы. Серия из пистолета даёт штрафную кость на каждый выстрел,
    /// залп автоматического оружия — нарастающий штраф по номеру проверки (стр. 111, 114).
    /// </summary>
    public FiringMode FiringMode { get; set; } = FiringMode.Single;

    /// <summary>Серия выстрелов из пистолета → штрафная кость</summary>
    public bool IsMultipleShot => FiringMode == FiringMode.PistolBurst;

    /// <summary>
    /// Номер проверки атаки при автоматической стрельбе в этом раунде, начиная с нуля.
    /// Каждая следующая проверка получает штрафную кость (стр. 114).
    /// </summary>
    public int AutofireCheckIndex { get; set; }

    /// <summary>Сколько патронов уходит на эту проверку атаки.</summary>
    public int ShotsFired { get; set; } = 1;

    /// <summary>Зарядка + выстрел в одном раунде → штрафная кость</summary>
    public bool IsReloadAndFire { get; set; }
}
