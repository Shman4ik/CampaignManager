using CampaignManager.Web.Components.Features.Scenarios.Model;
using CampaignManager.Web.Model;

namespace CampaignManager.Web.Components.Features.Campaigns.Models;

/// <summary>
///     Запись журнала кампании — одна игровая встреча. Боевой и погонный журналы живут только
///     в пределах сцены, а хронология «что было на прошлой сессии» нужна между встречами.
///     <para>
///         Текста два, и видны они разным людям: <see cref="Summary" /> — хроника для всего стола,
///         <see cref="KeeperNotes" /> — заметки Хранителя, которые игрокам не отдаются вовсе
///         (их вырезает <c>CampaignJournalService</c>, а не страница).
///     </para>
/// </summary>
public sealed class CampaignSession : BaseDataBaseEntity
{
    public Guid CampaignId { get; set; }

    public Campaign Campaign { get; set; } = null!;

    /// <summary>Дата встречи за столом (не игровая дата внутри истории).</summary>
    public DateOnly SessionDate { get; set; }

    /// <summary>Порядковый номер встречи в кампании. Предлагается следующий, но правится руками.</summary>
    public int Number { get; set; }

    public string? Title { get; set; }

    /// <summary>Хроника встречи: что произошло. Видна Хранителю и игрокам кампании.</summary>
    public string? Summary { get; set; }

    /// <summary>Заметки Хранителя: тайны, зацепки, планы. Игрокам не показываются.</summary>
    public string? KeeperNotes { get; set; }

    /// <summary>Сценарий, который шёл на встрече. Необязателен: встреча может быть и без него.</summary>
    public Guid? ScenarioId { get; set; }

    public Scenario? Scenario { get; set; }

    /// <summary>
    ///     На этой встрече закончился сценарий или глава кампании — момент для фазы развития
    ///     сыщиков (стр. 92). Только подсказка: журнал показывает ссылки на листы, а саму фазу
    ///     Хранитель проводит в листе, автоматики здесь нет.
    /// </summary>
    public bool ScenarioCompleted { get; set; }
}
