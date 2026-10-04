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

    /// <summary>«Пустой лист» игрока в кампании: страница с одной карточкой «Чистый лист» (сыщик сразу идёт в помощник).</summary>
    public static string Blank(Guid campaignId) => $"character/create/{campaignId}?blank=1";

    public static string Create(CharacterKind kind, Guid? campaignId, Guid? scenarioId) => kind is CharacterKind.Player && campaignId is { } campaign
        ? $"character/create/{campaign}" + Query(kind, null, scenarioId, null)
        : $"character/create/{RouteKind(kind) ?? "player"}" + Query(kind, campaignId, scenarioId, null, withKind: false);

    /// <summary>Куда вернуться без создания: сыщик — на главную, сценарий — в сценарий, НПС и прегены — в библиотеку.</summary>
    public static string Back(CharacterKind kind, Guid? scenarioId) => scenarioId is { } scenario
        ? Scenarios.ScenarioLinks.Workspace(scenario, kind is CharacterKind.Pregen ? Scenarios.ScenarioLinks.Tabs.Pregens : Scenarios.ScenarioLinks.Tabs.Npcs)
        : kind is CharacterKind.Player ? "" : "npcs";

    /// <summary>Шагов в помощнике: способ, пять шагов главы 3 и «Итог».</summary>
    public const int StepCount = (int)CreationStep.Summary + 1;

    /// <summary>Подпись шага — в степпере помощника, у Хранителя («шаг 4 из 7 — Навыки») и на кнопке «Продолжить создание».</summary>
    public static string StepTitle(CreationStep step) => step switch
    {
        CreationStep.Method => "Способ",
        CreationStep.Characteristics => "Характеристики",
        CreationStep.Occupation => "Профессия",
        CreationStep.Skills => "Навыки",
        CreationStep.Biography => "Биография",
        CreationStep.Gear => "Снаряжение",
        _ => "Итог",
    };

    /// <summary>«шаг 4 из 7 — Навыки» по номеру шага черновика (0–6).</summary>
    public static string StepText(int step)
    {
        var clamped = (CreationStep)Math.Clamp(step, 0, StepCount - 1);
        return $"шаг {(int)clamped + 1} из {StepCount} — {StepTitle(clamped)}";
    }

    /// <summary>Черновик помощника в <c>localStorage</c>: свой на каждое место (кампания, сценарий, библиотека).</summary>
    public static string DraftKey(CharacterKind kind, Guid? campaignId, Guid? scenarioId) =>
        $"cm.investigator-draft:{kind}:{campaignId?.ToString() ?? scenarioId?.ToString() ?? "library"}";

    public static string KindTitle(CharacterKind kind) => kind switch
    {
        CharacterKind.Npc => "НПС",
        CharacterKind.Pregen => "Готовый сыщик",
        _ => "Сыщик",
    };

    /// <summary>«Случайный сыщик» / «Случайный НПС» / «Случайный готовый сыщик» — ссылка на первом шаге помощника.</summary>
    public static string RandomTitle(CharacterKind kind) => kind is CharacterKind.Player
        ? "Случайный сыщик"
        : "Случайный " + (kind is CharacterKind.Npc ? "НПС" : "готовый сыщик");

    /// <summary>Куда вернуться и как это назвать в сообщении «Черновик сохранён — продолжить можно …».</summary>
    public static string ResumeHint(CharacterKind kind, Guid? scenarioId) => scenarioId is not null
        ? "из сценария"
        : kind is CharacterKind.Player ? "с главной" : "из библиотеки";

    public static string NewTitle(CharacterKind kind) => kind switch
    {
        CharacterKind.Npc => "Новый НПС",
        CharacterKind.Pregen => "Новый готовый сыщик",
        _ => "Новый сыщик",
    };

    /// <summary>Подзаголовок страницы создания: где окажется лист и эпоха — вместо цветной метки без легенды.</summary>
    public static string PlaceText(CreationContextDto context)
    {
        var place = context.ScenarioName is { } scenario ? $"Сценарий «{scenario}»"
            : context.CampaignName is { } campaign ? $"Кампания «{campaign}»"
            : context.Kind is CharacterKind.Player ? "Без кампании" : "Библиотека";
        return $"{place} · {EraTitle(context.Era)}";
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
