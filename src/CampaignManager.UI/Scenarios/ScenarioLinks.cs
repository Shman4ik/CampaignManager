using CampaignManager.Contracts.Scenarios;

namespace CampaignManager.UI.Scenarios;

/// <summary>
/// Адреса сценария — маршруты v1 (на них есть закладки и ссылки второго экрана): <c>/scenarios</c>, <c>/scenarios/new</c>,
/// <c>/scenarios/{id}</c> (рабочее место, вкладка — <c>?tab=</c>), <c>/scenarios/{id}/edit</c> (ведёт на вкладку «Описание»).
/// Режим игры — <c>?mode=play&amp;location=&amp;handout=&amp;run=</c> того же адреса (T2.5b); второй экран раздатки —
/// <c>/scenarios/{id}/handouts/{handoutId}</c>, статическая страница сервера.
/// </summary>
public static class ScenarioLinks
{
    public const string List = "scenarios";

    public const string New = "scenarios/new";

    /// <summary>Вкладки рабочего места — ключи <c>?tab=</c>.</summary>
    public static class Tabs
    {
        public const string Description = "description";
        public const string Text = "text";
        public const string Locations = "locations";
        public const string Checks = "checks";
        public const string Facts = "facts";
        public const string Handouts = "handouts";
        public const string Creatures = "creatures";
        public const string Items = "items";
        public const string Npcs = "npcs";
        public const string Pregens = "pregens";
    }

    public static string Workspace(Guid scenarioId, string? tab = null) =>
        tab is null or Tabs.Description ? $"scenarios/{scenarioId}" : $"scenarios/{scenarioId}?tab={tab}";

    /// <summary>Значение <c>?mode=</c> режима игры.</summary>
    public const string PlayMode = "play";

    /// <summary>
    /// Режим игры (T2.5b): открытая локация, показанная игрокам раздатка и выбранное прохождение (откуда сыщики для проверок) —
    /// в адресе, переходы — с <c>replace: true</c>: перезагрузка возвращает тот же экран, а «Назад» ведёт со сценария, а не по
    /// локациям.
    /// </summary>
    public static string Play(Guid scenarioId, Guid? locationId = null, Guid? handoutId = null, Guid? runId = null) =>
        $"scenarios/{scenarioId}?mode={PlayMode}"
        + (locationId is { } location ? $"&location={location}" : "")
        + (handoutId is { } handout ? $"&handout={handout}" : "")
        + (runId is { } run ? $"&run={run}" : "");

    /// <summary>Бой прохождения (T2.6d): <c>/combat?campaign=&amp;run=</c> — сцена в кампании прохождения стартует сразу с ним.</summary>
    public static string Combat(ScenarioRunDto run) => $"combat?campaign={run.CampaignId}&run={run.Id}";

    /// <summary>Второй экран раздатки — статическая страница сервера (телевизор, проектор, телефон игрока).</summary>
    public static string HandoutScreen(Guid scenarioId, Guid handoutId) => $"scenarios/{scenarioId}/handouts/{handoutId}";
}
