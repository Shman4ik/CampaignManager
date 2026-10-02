using CampaignManager.Contracts.Campaigns;

namespace CampaignManager.UI.Campaigns;

/// <summary>
/// Форма встречи журнала. Изменяемая — её правят поля модалки; на сервер уходит
/// <see cref="ToInput"/>, в черновик <c>localStorage</c> — тот же <see cref="CampaignSessionInput"/>.
/// </summary>
public sealed class SessionForm
{
    public DateOnly SessionDate { get; set; }
    public int Number { get; set; } = 1;
    public string? Title { get; set; }
    public string? Summary { get; set; }
    public string? KeeperNotes { get; set; }
    public Guid? RunId { get; set; }
    public bool ScenarioCompleted { get; set; }

    /// <summary>Выбор прохождения строкой: <c>select</c> не связывается с <c>Guid?</c> напрямую.</summary>
    public string RunIdText
    {
        get => RunId?.ToString() ?? "";
        set => RunId = Guid.TryParse(value, out var id) ? id : null;
    }

    public CampaignSessionInput ToInput() =>
        new(SessionDate, Number, Normalize(Title), Normalize(Summary), Normalize(KeeperNotes), RunId, ScenarioCompleted);

    public static SessionForm New(int number, DateOnly today) => new() { Number = number, SessionDate = today };

    public static SessionForm From(CampaignSessionDto session) => new()
    {
        SessionDate = session.SessionDate,
        Number = session.Number,
        Title = session.Title,
        Summary = session.Summary,
        KeeperNotes = session.KeeperNotes,
        RunId = session.RunId,
        ScenarioCompleted = session.ScenarioCompleted,
    };

    public static SessionForm From(CampaignSessionInput input) => new()
    {
        SessionDate = input.SessionDate,
        Number = input.Number,
        Title = input.Title,
        Summary = input.Summary,
        KeeperNotes = input.KeeperNotes,
        RunId = input.RunId,
        ScenarioCompleted = input.ScenarioCompleted,
    };

    /// <summary>Что не так с формой до отправки; <c>null</c> — можно сохранять. Сервер проверяет то же самое.</summary>
    public string? Validate()
    {
        if (Number is < CampaignLimits.MinSessionNumber or > CampaignLimits.MaxSessionNumber)
        {
            return $"Номер встречи — от {CampaignLimits.MinSessionNumber} до {CampaignLimits.MaxSessionNumber}.";
        }

        if (Title?.Trim().Length > CampaignLimits.SessionTitleLength)
        {
            return $"Заголовок — не длиннее {CampaignLimits.SessionTitleLength} символов.";
        }

        return Summary?.Length > CampaignLimits.SessionTextLength || KeeperNotes?.Length > CampaignLimits.SessionTextLength
            ? "Текст записи слишком длинный."
            : null;
    }

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
