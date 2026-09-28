using CampaignManager.Web.Model;

namespace CampaignManager.Web.Components.Features.Scenarios.Model;

/// <summary>
///     То, что <c>ScenarioDetailPage</c> прочитала в пререндере, — снимок для первого интерактивного
///     рендера (<c>[PersistentState]</c>, JSON). Поэтому граф обязан быть без циклов: сценарий — без
///     навигаций (<c>GetScenarioByIdAsync</c> их не грузит), у листов состава снята обратная ссылка
///     <c>ScenarioCasts</c> (<c>GetScenarioCastAsync</c>), прегены — без навигаций.
///     Добавишь сюда сущность с <c>Include</c> — проверь, что она не замыкается сама на себя.
/// </summary>
public sealed record ScenarioPagePrerender(
    Guid ScenarioId,
    Scenario Scenario,
    List<ScenarioNpc> Cast,
    List<CharacterStorageDto> Pregens,
    ScenarioAccess Access);
