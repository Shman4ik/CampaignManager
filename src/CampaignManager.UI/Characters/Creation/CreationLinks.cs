using CampaignManager.Contracts.Characters;
using CampaignManager.Contracts.Catalogs;
using CampaignManager.Core;
using CampaignManager.Core.Characters;

namespace CampaignManager.UI.Characters.Creation;

/// <summary>
/// Адреса создания листа — маршруты v1 (на них есть ссылки с главной и из сценария): <c>/character/create/{campaignId}</c>,
/// <c>/character/create/npc|pregen?campaignId=&amp;scenarioId=</c>, <c>/character/wizard?campaignId=&amp;scenarioId=&amp;kind=&amp;random=1</c>.
/// Помощник — именно <c>/character/wizard</c>, а не <c>/character/create/wizard</c>: <c>/character/create/{Kind}</c> со строковым
/// параметром перехватил бы любой третий сегмент (знание v1).
/// </summary>
public static class CreationLinks
{
    /// <summary>Вид из адреса: <c>npc</c> (и <c>template</c> старых ссылок), <c>pregen</c>; остальное — сыщик игрока.</summary>
    public static CharacterKind KindFromRoute(string? kind) => kind?.Trim().ToLowerInvariant() switch
    {
        "npc" or "template" => CharacterKind.Npc,
        "pregen" => CharacterKind.Pregen,
        _ => CharacterKind.Player,
    };

    public static string? RouteKind(CharacterKind kind) => kind switch
    {
        CharacterKind.Npc => "npc",
        CharacterKind.Pregen => "pregen",
        _ => null,
    };

    public static string Wizard(CharacterKind kind, Guid? campaignId, Guid? scenarioId, bool random = false) =>
        "character/wizard" + Query(kind, campaignId, scenarioId, random ? "random=1" : null);

    public static string Create(CharacterKind kind, Guid? campaignId, Guid? scenarioId) => kind is CharacterKind.Player && campaignId is { } campaign
        ? $"character/create/{campaign}" + Query(kind, null, scenarioId, null)
        : $"character/create/{RouteKind(kind) ?? "player"}" + Query(kind, campaignId, scenarioId, null, withKind: false);

    /// <summary>Куда вернуться без создания: сыщик — на главную, сценарий — в сценарий, НПС и прегены — в библиотеку.</summary>
    public static string Back(CharacterKind kind, Guid? scenarioId) => scenarioId is { } scenario
        ? Scenarios.ScenarioLinks.Workspace(scenario, kind is CharacterKind.Pregen ? Scenarios.ScenarioLinks.Tabs.Pregens : Scenarios.ScenarioLinks.Tabs.Npcs)
        : kind is CharacterKind.Player ? "" : "npcs";

    /// <summary>Черновик помощника в <c>localStorage</c>: свой на каждое место (кампания, сценарий, библиотека).</summary>
    public static string DraftKey(CharacterKind kind, Guid? campaignId, Guid? scenarioId) =>
        $"cm.investigator-draft:{kind}:{campaignId?.ToString() ?? scenarioId?.ToString() ?? "library"}";

    public static string KindTitle(CharacterKind kind) => kind switch
    {
        CharacterKind.Npc => "НПС",
        CharacterKind.Pregen => "Преген",
        _ => "Сыщик",
    };

    public static string NewTitle(CharacterKind kind) => kind switch
    {
        CharacterKind.Npc => "Новый НПС",
        CharacterKind.Pregen => "Новый преген",
        _ => "Новый сыщик",
    };

    /// <summary>Подзаголовок страницы создания: куда ляжет лист и эпоха — вместо цветной метки без легенды.</summary>
    public static string PlaceText(CreationContextDto context)
    {
        var place = context.ScenarioName is { } scenario ? $"сценарий «{scenario}»"
            : context.CampaignName is { } campaign ? $"кампания «{campaign}»"
            : context.Kind is CharacterKind.Player ? "без кампании" : "библиотека НПС и готовых сыщиков";
        return $"Куда ляжет: {place} · {EraTitle(context.Era)}";
    }

    public static string EraTitle(Era era) => era is Era.Modern ? "Наше время" : "1920-е";

    private static string Query(CharacterKind kind, Guid? campaignId, Guid? scenarioId, string? extra, bool withKind = true)
    {
        List<string> parts = [];
        if (campaignId is { } campaign)
            parts.Add($"campaignId={campaign}");
        if (scenarioId is { } scenario)
            parts.Add($"scenarioId={scenario}");
        if (withKind && RouteKind(kind) is { } routeKind)
            parts.Add($"kind={routeKind}");
        if (extra is not null)
            parts.Add(extra);
        return parts.Count == 0 ? "" : "?" + string.Join("&", parts);
    }
}

/// <summary>Профессия из справочника API как <see cref="OccupationDefinition"/> Core — по ней помощник собирает слоты.</summary>
public static class OccupationCatalogs
{
    public static OccupationDefinition From(OccupationDto dto) => new(dto.Id, dto.Name, dto.SkillPointsFormula)
    {
        CreditRatingMin = dto.CreditRatingMin,
        CreditRatingMax = dto.CreditRatingMax,
        Slots =
        [
            .. dto.Slots.Select(s => new OccupationSlotDefinition(s.Kind)
            {
                SkillId = s.SkillId,
                Specialization = s.Specialization,
                ChooseCount = s.ChooseCount,
                Options = s.Options,
            }),
        ],
    };
}
