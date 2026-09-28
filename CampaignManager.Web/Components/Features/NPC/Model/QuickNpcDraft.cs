using CampaignManager.Web.Components.Features.Characters.Model;

namespace CampaignManager.Web.Components.Features.NPC.Model;

/// <summary>
///     Форма «Быстрого НПС»: встречный констебль или бармен, которому нужны имя, род занятий,
///     пара чисел и боевой навык, а не полный лист сыщика. Лист из неё собирает
///     <c>QuickNpcRules.BuildCharacter</c>, сама форма ничего не считает.
/// </summary>
public sealed class QuickNpcDraft
{
    public string Name { get; set; } = string.Empty;

    public bool IsFemale { get; set; }

    public string Occupation { get; set; } = string.Empty;

    public int Age { get; set; } = 35;

    /// <summary>Восемь характеристик. По умолчанию — «средний человек», все по 50.</summary>
    public Dictionary<CharacteristicKey, int> Characteristics { get; set; } = [];

    public int Luck { get; set; } = 50;

    /// <summary>Ключевые навыки — три-пять строк, остальное остаётся на базовых значениях листа.</summary>
    public List<QuickNpcSkill> Skills { get; set; } = [];

    public QuickNpcCombatLevel CombatLevel { get; set; } = QuickNpcCombatLevel.Novice;

    /// <summary>Оружие из каталога; в лист уходит копия (<c>WeaponFactory.CopyForCharacter</c>).</summary>
    public Guid? WeaponId { get; set; }

    /// <summary>Навык, который форма добавила под выбранное оружие, — чтобы убрать его при смене оружия.</summary>
    public string? WeaponSkillName { get; set; }

    /// <summary>Короткая заметка «кто он и что знает» — ложится в предысторию листа.</summary>
    public string? Note { get; set; }

    /// <summary>Типаж, из которого заполнена форма; только для подсветки кнопки.</summary>
    public string? ArchetypeKey { get; set; }

    /// <summary>Кампания-владелец. <c>null</c> — общая библиотека НПС.</summary>
    public Guid? CampaignId { get; set; }

    /// <summary>Роль в сценарии — только когда НПС создаётся из сценария.</summary>
    public NpcRole Role { get; set; } = NpcRole.Neutral;

    /// <summary>Сколько таких в сценарии (три одинаковых громилы — одна строка).</summary>
    public int Count { get; set; } = 1;
}

public sealed class QuickNpcSkill
{
    public string Name { get; set; } = string.Empty;

    public int Value { get; set; }
}

/// <summary>
///     Боевой навык персонажа Хранителя, придуманного по ходу игры (гл. 10, стр. 187):
///     неопытный — 25%, трактирный забияка или наёмный громила — 40%, профессиональный убийца — 70%.
/// </summary>
public enum QuickNpcCombatLevel
{
    Novice = 25,
    Brawler = 40,
    Professional = 70
}
