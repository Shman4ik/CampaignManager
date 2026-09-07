using CampaignManager.Web.Components.Features.Bestiary.Model;
using CampaignManager.Web.Components.Features.Characters.Model;

namespace CampaignManager.Web.Components.Features.Combat.Model;

/// <summary>
///     Откуда взят участник. Раньше каждый источник жил в своей вкладке со своей семантикой,
///     теперь это просто метка строки в общем списке.
/// </summary>
public enum ParticipantSourceKind
{
    /// <summary>Активный персонаж игрока из выбранной кампании.</summary>
    CampaignCharacter = 0,

    /// <summary>НПС, занятый в выбранном сценарии (роль и количество — из состава сценария).</summary>
    ScenarioNpc = 1,

    /// <summary>НПС из библиотеки или кампании, не занятый в сценарии.</summary>
    LibraryNpc = 2,

    /// <summary>Незабронированный преген выбранного сценария.</summary>
    ScenarioPregen = 3,

    /// <summary>Монстр, добавленный в сценарий.</summary>
    ScenarioCreature = 4,

    /// <summary>Существо из бестиария.</summary>
    BestiaryCreature = 5
}

/// <summary>
///     Строка в списке «добавить участника»: годится и бою, и погоне, потому что несёт
///     исходную сущность, сторону и количество, а не готового участника.
/// </summary>
public sealed class ParticipantOption
{
    public required string Name { get; init; }

    public required ParticipantSourceKind SourceKind { get; init; }

    /// <summary>Лист персонажа — для персонажей игроков, НПС и прегенов.</summary>
    public Character? Character { get; init; }

    /// <summary>Существо — для монстров сценария и бестиария.</summary>
    public Creature? Creature { get; init; }

    /// <summary>Сторона по умолчанию: из роли НПС в сценарии, иначе по источнику.</summary>
    public CombatSide Side { get; init; } = CombatSide.Enemy;

    /// <summary>Сколько одинаковых участников добавляется разом (состав сценария).</summary>
    public int Count { get; init; } = 1;

    /// <summary>Короткая характеристика для правой колонки строки (ЛВК или СКО).</summary>
    public string Detail { get; init; } = "";
}
