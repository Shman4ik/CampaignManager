namespace CampaignManager.Contracts;

/// <summary>
/// Префикс API. Совместимость — только наращиванием: сломанный контракт сломает
/// у игроков мобильное приложение, которое ещё не обновилось.
/// </summary>
public static class ApiRoutes
{
    public const string Prefix = "/api/v1";
}
