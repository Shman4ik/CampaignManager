using Xunit;

namespace CampaignManager.Server.Tests;

/// <summary>
/// Тестовая база (D7): строка подключения из <c>CM_TEST_DB</c>. Локально — Postgres в <c>wslc</c>,
/// в CI — сервис-контейнер. Боевую базу и ветку Neon <c>dev</c> сюда не подставлять.
/// </summary>
public static class TestDatabase
{
    public const string Variable = "CM_TEST_DB";

    /// <summary>Заглушка, когда базы нет: сервер стартует, а до запроса к базе дело не доходит.</summary>
    public const string Unreachable = "Host=127.0.0.1;Port=1;Database=none;Username=none;Password=none";

    public static string? ConnectionString =>
        Environment.GetEnvironmentVariable(Variable) is { Length: > 0 } value ? value : null;

    public static void SkipIfMissing() =>
        Assert.SkipWhen(ConnectionString is null, $"Нет {Variable}: тест с базой пропущен.");
}
