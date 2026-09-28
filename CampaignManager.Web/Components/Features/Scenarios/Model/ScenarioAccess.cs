namespace CampaignManager.Web.Components.Features.Scenarios.Model;

/// <summary>
///     Что текущему пользователю можно делать со сценарием. Считает только
///     <c>ScenarioService.GetAccessAsync</c> — правила живут там в одном месте
///     (см. <c>Features/Scenarios/CLAUDE.md</c>, «Права»), страницы лишь прячут по нему кнопки.
/// </summary>
public readonly record struct ScenarioAccess(bool CanEdit, bool CanDelete)
{
    /// <summary>Ни править, ни удалять: игрок, аноним или чужой сценарий кампании.</summary>
    public static ScenarioAccess None => default;
}
