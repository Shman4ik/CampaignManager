namespace CampaignManager.Contracts.Campaigns;

/// <summary>
/// Кампании, участники, журнал встреч и главная. Ошибки — <see cref="HttpRequestException"/> с текстом
/// ProblemDetails и кодом: 404 — кампании нет или она не видна, 403 — видна, но нельзя (и вступить второй
/// раз или в завершённую), 400 — форма,
/// 409 — так нельзя по смыслу (убрать Хранителя, гонка двух вступлений).
/// </summary>
public interface ICampaignsApi
{
    Task<HomeDto> GetHomeAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CampaignSummaryDto>> GetCampaignsAsync(CancellationToken cancellationToken = default);

    Task<CampaignDetailsDto> GetCampaignAsync(Guid campaignId, CancellationToken cancellationToken = default);

    /// <summary>Только Хранитель по роли; создатель становится Хранителем кампании.</summary>
    Task<CampaignSummaryDto> CreateCampaignAsync(CampaignInput input, CancellationToken cancellationToken = default);

    Task<CampaignSummaryDto> UpdateCampaignAsync(Guid campaignId, CampaignInput input, CancellationToken cancellationToken = default);

    Task DeleteCampaignAsync(Guid campaignId, CancellationToken cancellationToken = default);

    Task<CampaignDetailsDto> JoinAsync(Guid campaignId, JoinCampaignRequest request, CancellationToken cancellationToken = default);

    Task<CampaignMemberDto> UpdateMemberAsync(Guid campaignId, Guid userId, UpdateMemberRequest request, CancellationToken cancellationToken = default);

    /// <summary>Выйти самому (свой <paramref name="userId"/>) или исключить игрока (Хранитель кампании).</summary>
    Task RemoveMemberAsync(Guid campaignId, Guid userId, CancellationToken cancellationToken = default);

    Task<CampaignJournalDto> GetJournalAsync(Guid campaignId, CancellationToken cancellationToken = default);

    Task<CampaignSessionDto> AddSessionAsync(Guid campaignId, CampaignSessionInput input, CancellationToken cancellationToken = default);

    Task<CampaignSessionDto> UpdateSessionAsync(Guid campaignId, Guid sessionId, CampaignSessionInput input, CancellationToken cancellationToken = default);

    Task DeleteSessionAsync(Guid campaignId, Guid sessionId, CancellationToken cancellationToken = default);
}
