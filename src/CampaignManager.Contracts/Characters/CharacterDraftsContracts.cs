using CampaignManager.Core.Characters;

namespace CampaignManager.Contracts.Characters;

/// <summary>
/// Черновик помощника создания сыщика игрока в кампании — на сервере: продолжить с любого устройства, Хранитель видит шаг.
/// Свой черновик — <see cref="MinePattern"/> (пишет только сам игрок), чужой читают Хранитель кампании и администратор —
/// <see cref="PlayerPattern"/>.
/// </summary>
public static class CharacterDraftsRoutes
{
    /// <summary>
    /// Мой черновик в кампании. <c>GET</c> → <see cref="CharacterDraftDto"/> (404 — черновика нет или кампания не видна),
    /// <c>PUT</c> <see cref="InvestigatorDraft"/> → <see cref="CharacterSavedDto"/>: первый — без <c>If-Match</c>, следующие — с
    /// версией (без неё 428, устаревшая — 409 <c>stale</c>); <c>DELETE</c> → 204 («Начать заново»; черновика нет — тоже 204).
    /// </summary>
    public const string MinePattern = ApiRoutes.Prefix + "/campaigns/{campaignId:guid}/character-draft";

    /// <summary><c>GET</c> — черновик игрока кампании: <see cref="CharacterDraftDto"/>.</summary>
    public const string PlayerPattern = ApiRoutes.Prefix + "/campaigns/{campaignId:guid}/character-drafts/{userId:guid}";

    public static string Mine(Guid campaignId) => $"{ApiRoutes.Prefix}/campaigns/{campaignId}/character-draft";

    public static string Player(Guid campaignId, Guid userId) => $"{ApiRoutes.Prefix}/campaigns/{campaignId}/character-drafts/{userId}";
}

/// <summary>Черновик целиком: документ помощника и версия для следующего <c>If-Match</c>.</summary>
public sealed record CharacterDraftDto(Guid CampaignId, Guid OwnerId, InvestigatorDraft Draft, uint Version, DateTimeOffset UpdatedAt);

/// <summary>
/// Что видно о черновике без документа — строка «Создаёт сыщика: шаг 4 из 7 — Навыки · 2 часа назад» у Хранителя и
/// «Продолжить создание (шаг 4 из 7)» у самого игрока.
/// </summary>
/// <param name="Step">Шаг помощника — номер <see cref="CreationStep"/> (0 — «Способ», 6 — «Итог»).</param>
/// <param name="UpdatedAt">Когда черновик последний раз записан (UTC).</param>
public sealed record CharacterDraftSummaryDto(int Step, DateTimeOffset UpdatedAt);

/// <summary>Черновик сыщика игрока в кампании (<see cref="CharacterDraftsRoutes"/>).</summary>
public interface ICharacterDraftsApi
{
    /// <summary>Мой черновик в кампании; <c>null</c> — его нет.</summary>
    Task<CharacterDraftDto?> GetMineAsync(Guid campaignId, CancellationToken cancellationToken = default);

    /// <summary>Черновик игрока — Хранителю кампании и администратору (и самому игроку); 404 — нет или не видно.</summary>
    Task<CharacterDraftDto> GetAsync(Guid campaignId, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Записать мой черновик. <paramref name="version"/> — версия, на которой его правили; <c>null</c> — черновика на сервере
    /// ещё нет. Отказы — <see cref="Platform.ApiException"/>: 409 <c>stale</c> (записан или стёрт с другого устройства), 400, 403.
    /// </summary>
    Task<CharacterSavedDto> SaveAsync(Guid campaignId, InvestigatorDraft draft, uint? version, CancellationToken cancellationToken = default);

    /// <summary>Стереть мой черновик («Начать заново»); черновика нет — не ошибка.</summary>
    Task DeleteAsync(Guid campaignId, CancellationToken cancellationToken = default);
}
