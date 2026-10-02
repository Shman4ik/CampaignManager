using CampaignManager.Core.Documents;

namespace CampaignManager.Core.Catalogs;

/// <summary>
/// Статблок твари — документ <c>creatures.statblock</c> и <c>scenario_creatures.statblock</c> (SCHEMA,
/// «Документы»). Объединяет четыре jsonb-колонки v1 (характеристики, атаки, навыки, особые способности).
/// Наследие v1 не переносится: словарь <c>CombatDescriptions</c>, <c>ImageUrl</c>, ключи
/// <c>Appearance</c>/<c>Education</c>/<c>Luck</c>/<c>Constitutions</c>.
/// </summary>
public sealed record Statblock : DocumentPart
{
    /// <summary>Текущая версия документа (<c>*_statblock_version</c>).</summary>
    public const int CurrentVersion = 1;

    public StatValue Str { get; set; } = new();
    public StatValue Con { get; set; } = new();
    public StatValue Siz { get; set; } = new();
    public StatValue Dex { get; set; } = new();
    public StatValue Int { get; set; } = new();
    public StatValue Pow { get; set; } = new();

    public int HitPoints { get; set; }

    public int MagicPoints { get; set; }

    /// <summary>Средний бонус к урону строкой книги: «+1D4», «+3D6».</summary>
    public string DamageBonus { get; set; } = "0";

    public int Build { get; set; }

    public CreatureSpeed Speed { get; set; } = new();

    /// <summary>Атак за раунд — столько же защит до численного превосходства.</summary>
    public int AttacksPerRound { get; set; } = 1;

    public string? AttacksPerRoundNote { get; set; }

    public int Armor { get; set; }

    public string? ArmorNote { get; set; }

    public int Dodge { get; set; }

    /// <summary>Потеря рассудка «успех/провал» — разбирает <see cref="SanityLossFormula"/>.</summary>
    public string SanityLoss { get; set; } = "";

    /// <summary>Инициатива; 0 — по ЛВК.</summary>
    public int Initiative { get; set; }

    public List<CreatureAttack> Attacks { get; set; } = [];

    public List<CreatureSkill> Skills { get; set; } = [];

    public List<SpecialAbility> SpecialAbilities { get; set; } = [];
}

/// <summary>Характеристика твари: среднее и кости, которыми её бросают («3D6×5»).</summary>
public sealed record StatValue : DocumentPart
{
    public int Value { get; set; }

    public string? Dice { get; set; }
}

public sealed record CreatureSpeed : DocumentPart
{
    public int Move { get; set; }

    public int? Swim { get; set; }

    public int? Fly { get; set; }

    public string? Note { get; set; }
}

/// <summary>Какая это атака твари (стр. 278–279).</summary>
public enum CreatureAttackKind
{
    /// <summary>Когти, щупальца, укусы.</summary>
    Melee,

    /// <summary>Молниемёт Старца, плевок дхоула.</summary>
    Ranged,

    /// <summary>Захват, затаптывание: исход решает разница Комплекции, а не урон.</summary>
    Maneuver,

    /// <summary>Особая атака — читать описание.</summary>
    Special,
}

/// <summary>Как к урону атаки добавляется бонус к урону.</summary>
public enum CreatureDamageBonusMode
{
    /// <summary>«урон 1d3» — без бонуса.</summary>
    None,

    /// <summary>«урон 2d6 + БкУ».</summary>
    Full,

    /// <summary>«урон 2d3 + ½ БкУ».</summary>
    Half,

    /// <summary>«урон равен БкУ» — своих костей нет.</summary>
    OnlyBonus,
}

public sealed record CreatureAttack : DocumentPart
{
    public string Name { get; set; } = "";

    public int SkillValue { get; set; } = 50;

    /// <summary>Формула урона; «0», если весь урон — бонус к урону.</summary>
    public string Damage { get; set; } = "";

    public CreatureAttackKind Kind { get; set; }

    public CreatureDamageBonusMode DamageBonusMode { get; set; }
}

public sealed record CreatureSkill : DocumentPart
{
    public Guid? SkillId { get; set; }

    public string Name { get; set; } = "";

    public int Value { get; set; }
}

public sealed record SpecialAbility : DocumentPart
{
    public string Name { get; set; } = "";

    public string Text { get; set; } = "";
}
