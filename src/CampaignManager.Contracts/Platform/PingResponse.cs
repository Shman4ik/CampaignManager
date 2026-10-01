namespace CampaignManager.Contracts.Platform;

/// <summary>
/// Ответ <c>GET /api/v1/ping</c>: сервер жив и дотягивается до базы.
/// </summary>
/// <param name="ServerTime">Время на сервере в момент ответа.</param>
/// <param name="DatabaseTime">Время, которое вернул Postgres (<c>now()</c>).</param>
/// <param name="DatabaseLatencyMs">Сколько занял запрос к базе.</param>
/// <param name="ServerVersion">Версия сборки сервера.</param>
/// <param name="Environment">Окружение сервера: Development или Production.</param>
public sealed record PingResponse(
    DateTimeOffset ServerTime,
    DateTimeOffset DatabaseTime,
    double DatabaseLatencyMs,
    string ServerVersion,
    string Environment);
