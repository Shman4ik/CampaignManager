using CampaignManager.Core;
using CampaignManager.Core.Campaigns;

namespace CampaignManager.Contracts.Scenarios;

/// <summary>
/// Прохождения (T2.5c): сценарий библиотеки играют в кампании, содержимое не копируется. Ваншот — кампания вида
/// <see cref="CampaignKind.OneShot"/> с одним прохождением и открытой записью: игрок бронирует прегена сценария и получает
/// копию его листа. Создают прохождение адреса сценария (<see cref="ScenariosRoutes.RunsPattern"/>,
/// <see cref="ScenariosRoutes.OneShotPattern"/>), правят и бронируют — эти.
/// </summary>
public static class RunsRoutes
{
    public const string Runs = ApiRoutes.Prefix + "/runs";

    /// <summary><c>PUT</c> <see cref="RunInput"/> — одна форма прохождения (состояние, дата, анонс, запись); <c>DELETE</c>.</summary>
    public const string RunPattern = Runs + "/{runId:guid}";

    /// <summary><c>POST</c> <see cref="ReserveRequest"/> — забронировать прегена → <see cref="ReservationDto"/>.</summary>
    public const string ReservationsPattern = RunPattern + "/reservations";

    /// <summary><c>DELETE</c> — снять бронь: сам игрок или Хранитель кампании прохождения.</summary>
    public const string ReservationPattern = ReservationsPattern + "/{pregenId:guid}";

    public static string Run(Guid runId) => $"{Runs}/{runId}";

    public static string Reservations(Guid runId) => $"{Run(runId)}/reservations";

    public static string Reservation(Guid runId, Guid pregenId) => $"{Reservations(runId)}/{pregenId}";
}

/// <summary>«Играть в кампании»: сценарий проходят в кампании, которую ведёт вошедший.</summary>
public sealed record PlayInCampaignRequest(Guid CampaignId, DateTimeOffset? ScheduledAt = null);

/// <summary>
/// «Объявить ваншот»: новая кампания вида <see cref="CampaignKind.OneShot"/>, вошедший — её Хранитель, прохождение с открытой
/// записью. <see cref="CampaignName"/> пусто — «Сценарий (ваншот)»; <see cref="Era"/> пусто — эпоха сценария.
/// </summary>
public sealed record AnnounceOneShotRequest(string? CampaignName, DateTimeOffset? ScheduledAt, string? Announcement, Era? Era = null);

/// <summary>
/// Одна форма прохождения — анонс больше не в сценарии (в v1 он правился в двух формах с разной обработкой даты).
/// Завершённое прохождение запись закрывает само.
/// </summary>
public sealed record RunInput(ScenarioRunStatus Status, DateTimeOffset? ScheduledAt, string? Announcement, bool SignupOpen);

public sealed record ReserveRequest(Guid PregenId);

/// <summary>Бронь взята: <see cref="CharacterId"/> — копия листа прегена у игрока в кампании прохождения.</summary>
public sealed record ReservationDto(Guid RunId, Guid PregenId, Guid CharacterId, Guid CampaignId);

/// <summary>
/// Прохождения и брони. Отказы — <see cref="Platform.ApiException"/>: 404 — не видно (чужая кампания, чужая бронь), 403 —
/// видно, но нельзя (запись закрыта, снять чужую бронь игроку), 400 — форма, 409 <c>conflict</c>/<c>duplicate</c> — так
/// нельзя по смыслу (преген занят, вы уже забронировали, у вас уже есть сыщик, игра сыграна).
/// Список прохождений сценария — <see cref="IScenariosApi.ListRunsAsync"/>.
/// </summary>
public interface IRunsApi
{
    /// <summary>Прохождение без копии содержимого в кампании, которую вы ведёте.</summary>
    Task<ScenarioRunDto> PlayInCampaignAsync(Guid scenarioId, PlayInCampaignRequest request, CancellationToken cancellationToken = default);

    /// <summary>Кампания-ваншот, Хранитель-участник и прохождение с открытой записью — одной транзакцией.</summary>
    Task<ScenarioRunDto> AnnounceOneShotAsync(Guid scenarioId, AnnounceOneShotRequest request, CancellationToken cancellationToken = default);

    Task<ScenarioRunDto> UpdateAsync(Guid runId, RunInput input, CancellationToken cancellationToken = default);

    /// <summary>Удалить прохождение: брони уходят с ним, копии листов остаются у игроков, встречи журнала — без ссылки.</summary>
    Task DeleteAsync(Guid runId, CancellationToken cancellationToken = default);

    /// <summary>Забронировать прегена сценария: копия листа вам в кампанию прохождения (не участник — станете им).</summary>
    Task<ReservationDto> ReserveAsync(Guid runId, Guid pregenId, CancellationToken cancellationToken = default);

    /// <summary>Снять бронь: копия листа уходит в архив, преген свободен.</summary>
    Task ReleaseAsync(Guid runId, Guid pregenId, CancellationToken cancellationToken = default);
}

/// <summary>Пределы полей прохождения.</summary>
public static class RunLimits
{
    public const int AnnouncementLength = 20000;
}
