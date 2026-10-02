namespace CampaignManager.UI.Scenarios;

/// <summary>
/// Адреса сценария — маршруты v1 (на них есть закладки и ссылки второго экрана): <c>/scenarios</c>, <c>/scenarios/new</c>,
/// <c>/scenarios/{id}</c> (рабочее место, вкладка — <c>?tab=</c>), <c>/scenarios/{id}/edit</c> (ведёт на вкладку «Описание»).
/// Режим игры — <c>?mode=play&amp;location=&amp;handout=</c> того же адреса (T2.5b): параметры зарезервированы, рабочее
/// место их не трогает.
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

    /// <summary>Режим игры (T2.5b): открытая локация и показанная раздатка — в адресе, переходы — с <c>replace: true</c>.</summary>
    public static string Play(Guid scenarioId, Guid? locationId = null, Guid? handoutId = null) =>
        $"scenarios/{scenarioId}?mode=play"
        + (locationId is { } location ? $"&location={location}" : "")
        + (handoutId is { } handout ? $"&handout={handout}" : "");
}
