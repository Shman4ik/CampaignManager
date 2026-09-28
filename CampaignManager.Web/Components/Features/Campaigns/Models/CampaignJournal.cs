using System.ComponentModel.DataAnnotations;

namespace CampaignManager.Web.Components.Features.Campaigns.Models;

/// <summary>
///     Журнал кампании в том виде, в каком его можно показать текущему пользователю.
///     Строит его только <c>CampaignJournalService</c>: он же решает, достаются ли читателю
///     заметки Хранителя и чьи листы попадают в подсказку о фазе развития.
/// </summary>
public sealed record CampaignJournal(
    Guid CampaignId,
    string CampaignName,
    bool CanEdit,
    int NextNumber,
    IReadOnlyList<CampaignSessionView> Sessions,
    IReadOnlyList<JournalScenarioOption> Scenarios,
    IReadOnlyList<JournalInvestigator> Investigators);

/// <summary>
///     Строка журнала. <see cref="KeeperNotes" /> у игрока всегда <c>null</c> — сервис не отдаёт
///     заметки Хранителя тем, кто не может их править.
/// </summary>
public sealed record CampaignSessionView(
    Guid Id,
    int Number,
    DateOnly SessionDate,
    string? Title,
    string? Summary,
    string? KeeperNotes,
    Guid? ScenarioId,
    string? ScenarioName,
    bool ScenarioCompleted);

/// <summary>Сценарий, который можно привязать к встрече.</summary>
public sealed record JournalScenarioOption(Guid Id, string Name);

/// <summary>Лист сыщика для ссылки «открыть фазу развития».</summary>
public sealed record JournalInvestigator(Guid CharacterId, string Name, string PlayerName);

/// <summary>Форма добавления и правки встречи.</summary>
public sealed class CampaignSessionInput
{
    /// <summary><c>null</c> — новая встреча.</summary>
    public Guid? Id { get; set; }

    public DateOnly SessionDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);

    [Range(1, 9999, ErrorMessage = "Номер встречи — от 1 до 9999")]
    public int Number { get; set; } = 1;

    [StringLength(CampaignJournalLimits.TitleLength, ErrorMessage = "Заголовок — не длиннее 200 символов")]
    public string? Title { get; set; }

    [StringLength(CampaignJournalLimits.TextLength, ErrorMessage = "Хроника слишком длинная")]
    public string? Summary { get; set; }

    [StringLength(CampaignJournalLimits.TextLength, ErrorMessage = "Заметки слишком длинные")]
    public string? KeeperNotes { get; set; }

    public Guid? ScenarioId { get; set; }

    public bool ScenarioCompleted { get; set; }

    public static CampaignSessionInput From(CampaignSessionView session) => new()
    {
        Id = session.Id,
        SessionDate = session.SessionDate,
        Number = session.Number,
        Title = session.Title,
        Summary = session.Summary,
        KeeperNotes = session.KeeperNotes,
        ScenarioId = session.ScenarioId,
        ScenarioCompleted = session.ScenarioCompleted
    };
}

/// <summary>Пределы полей журнала: одни и те же для формы, сервиса и схемы базы.</summary>
public static class CampaignJournalLimits
{
    public const int TitleLength = 200;
    public const int TextLength = 20000;
}
