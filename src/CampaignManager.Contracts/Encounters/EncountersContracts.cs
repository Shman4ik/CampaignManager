using CampaignManager.Core.Encounters;

namespace CampaignManager.Contracts.Encounters;

/// <summary>
/// Сцены — бой и погоня (T2.6). Состояние — документ Core (<see cref="EncounterState"/>), пишется целиком с <c>If-Match</c>:
/// сцену ведут с двух устройств (iPad за столом и ноутбук), и устаревшая запись получает 409, а не затирает.
/// </summary>
public static class EncountersRoutes
{
    /// <summary>
    /// <c>GET ?kind=</c> — активные сцены вошедшего Хранителя: <see cref="EncounterSummaryDto"/>;
    /// <c>POST</c> <see cref="StartEncounterRequest"/> → 201 <see cref="EncounterDto"/>.
    /// </summary>
    public const string Encounters = ApiRoutes.Prefix + "/encounters";

    /// <summary><c>GET</c> — <see cref="EncounterDto"/> и <c>ETag</c> с версией.</summary>
    public const string EncounterPattern = Encounters + "/{encounterId:guid}";

    /// <summary><c>PUT</c> <see cref="EncounterState"/> с <c>If-Match</c> → <see cref="EncounterSavedDto"/>.</summary>
    public const string StatePattern = EncounterPattern + "/state";

    /// <summary><c>PUT</c> <see cref="SetEncounterRunRequest"/> с <c>If-Match</c> — прохождение сцены (<c>run_id</c>) → <see cref="EncounterSavedDto"/>.</summary>
    public const string RunPattern = EncounterPattern + "/run";

    /// <summary><c>POST</c> с <c>If-Match</c> — завершить сцену (статус <c>Finished</c>): место под новую освобождается.</summary>
    public const string FinishPattern = EncounterPattern + "/finish";

    public static string Active(EncounterKind? kind) => kind is { } k ? $"{Encounters}?kind={k}" : Encounters;

    public static string Encounter(Guid encounterId) => $"{Encounters}/{encounterId}";

    public static string State(Guid encounterId) => $"{Encounter(encounterId)}/state";

    public static string Run(Guid encounterId) => $"{Encounter(encounterId)}/run";

    public static string Finish(Guid encounterId) => $"{Encounter(encounterId)}/finish";
}

/// <summary>Пределы документа сцены — одни для сервиса и сообщений.</summary>
public static class EncounterLimits
{
    /// <summary>Документ целиком (журнал ограничен ядром, участники — тоже); с запасом на снимки участников.</summary>
    public const int MaxStateBytes = 1024 * 1024;
}

/// <summary>Сцена целиком: документ и то, что о ней знает строка.</summary>
public sealed class EncounterDto
{
    public Guid Id { get; set; }

    public EncounterKind Kind { get; set; }

    public EncounterStatus Status { get; set; }

    public Guid? CampaignId { get; set; }

    public string? CampaignName { get; set; }

    /// <summary>Прохождение сценария, по которому идёт сцена (бой выбирает сценарий по нему); нет — сцена сама по себе.</summary>
    public Guid? RunId { get; set; }

    /// <summary>Версия строки (<c>xmin</c>) — для <c>If-Match</c>.</summary>
    public uint Version { get; set; }

    public EncounterState State { get; set; } = new();

    public DateTimeOffset UpdatedAt { get; set; }
}

/// <summary>
/// Строка списка активных сцен: продолжить начатое. <paramref name="ParticipantNames"/> — имена участников по порядку
/// добавления: две погони одной кампании (разделившиеся) различают по тому, кто в них бежит.
/// </summary>
public sealed record EncounterSummaryDto(
    Guid Id,
    EncounterKind Kind,
    Guid? CampaignId,
    string? CampaignName,
    int Round,
    int ParticipantCount,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<string> ParticipantNames,
    string? Name = null,
    bool Ended = false);

/// <summary>Новая сцена: вид и кампания (null — вне кампании). У Хранителя один активный бой на кампанию; погонь — сколько угодно.</summary>
/// <param name="RunId">Прохождение сценария в этой кампании (из режима игры сценария); без кампании — 400.</param>
public sealed record StartEncounterRequest(EncounterKind Kind, Guid? CampaignId, Guid? RunId = null);

/// <summary>Привязать сцену к прохождению сценария её кампании или снять привязку (<c>null</c>).</summary>
public sealed record SetEncounterRunRequest(Guid? RunId);

public sealed record EncounterSavedDto(uint Version, DateTimeOffset UpdatedAt);

/// <summary>
/// Сцены. Ошибки — <see cref="Platform.ApiException"/>: 404 — сцены нет или её ведёт другой (неразличимо), 403 — начать
/// сцену нельзя (не Хранитель, чужая кампания), 409 <c>conflict</c> — такая сцена уже идёт, 409 <c>stale</c> — состояние
/// изменили на другом устройстве, 428 — без версии, 400 — документ не прошёл проверку.
/// </summary>
public interface IEncountersApi
{
    Task<IReadOnlyList<EncounterSummaryDto>> ListActiveAsync(EncounterKind? kind, CancellationToken cancellationToken = default);

    Task<EncounterDto> StartAsync(StartEncounterRequest request, CancellationToken cancellationToken = default);

    Task<EncounterDto> GetAsync(Guid encounterId, CancellationToken cancellationToken = default);

    Task<EncounterSavedDto> SaveStateAsync(Guid encounterId, EncounterState state, uint version, CancellationToken cancellationToken = default);

    /// <summary>Привязать сцену к прохождению (или снять): версия строки меняется — вернётся новая.</summary>
    Task<EncounterSavedDto> SetRunAsync(Guid encounterId, Guid? runId, uint version, CancellationToken cancellationToken = default);

    Task<EncounterSavedDto> FinishAsync(Guid encounterId, uint version, CancellationToken cancellationToken = default);
}
