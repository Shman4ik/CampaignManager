using CampaignManager.Web.Components.Features.Characters.Model;
using CampaignManager.Web.Model;

namespace CampaignManager.Web.Components.Features.Scenarios.Model;

/// <summary>
///     Участие НПС в сценарии: связь между сценарием и листом персонажа.
///     <para>
///         Заменяет прежнее «привязывание» НПС копированием строки. Лист остаётся один,
///         правки Хранителя видны во всех сценариях, где НПС занят, а роль и количество
///         (три одинаковых громилы — одна связь с <see cref="Count" /> = 3) живут здесь,
///         потому что в разных сценариях они разные.
///     </para>
/// </summary>
public sealed class ScenarioNpc : BaseDataBaseEntity
{
    /// <summary>
    ///     Сценарий, в котором занят НПС.
    /// </summary>
    public required Guid ScenarioId { get; set; }

    /// <summary>
    ///     Навигационное свойство к сценарию.
    /// </summary>
    public Scenario? Scenario { get; set; }

    /// <summary>
    ///     Лист НПС. Всегда <c>CharacterKind.Npc</c>.
    /// </summary>
    public required Guid CharacterId { get; set; }

    /// <summary>
    ///     Навигационное свойство к листу НПС.
    /// </summary>
    public CharacterStorageDto? Character { get; set; }

    /// <summary>
    ///     Роль НПС именно в этом сценарии.
    /// </summary>
    public NpcRole Role { get; set; } = NpcRole.Neutral;

    /// <summary>
    ///     Сколько одинаковых НПС выходит на сцену. Боевой помощник добавляет столько
    ///     участников, нумеруя их.
    /// </summary>
    public int Count { get; set; } = 1;

    /// <summary>
    ///     Заметка Хранителя об этом появлении НПС.
    /// </summary>
    public string? Notes { get; set; }
}
