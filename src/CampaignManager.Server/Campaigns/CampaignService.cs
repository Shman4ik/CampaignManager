using CampaignManager.Contracts.Campaigns;
using CampaignManager.Core;
using CampaignManager.Core.Campaigns;
using CampaignManager.Data;
using CampaignManager.Data.Campaigns;
using CampaignManager.Server.Access;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CampaignManager.Server.Campaigns;

/// <summary>
/// Кампании и участники. Права — <see cref="AccessPolicy"/> в каждом методе: создать — Хранитель по роли,
/// править и удалять — Хранитель кампании или администратор, вступить — любой в незавершённую кампанию.
/// Хранитель — участник с ролью <see cref="CampaignRole.Keeper"/>, свою кампанию он видит сразу.
/// </summary>
public sealed class CampaignService(
    CmDbContext dbContext,
    AccessPolicy access,
    CurrentUser currentUser,
    ILogger<CampaignService> logger)
{
    /// <summary>Кампании, где я участник; администратору — все (он правит и чужие). Новые сверху.</summary>
    public async Task<IReadOnlyList<CampaignSummaryDto>> ListAsync(CancellationToken cancellationToken)
    {
        var user = await RequireUserAsync(cancellationToken);
        var campaigns = user.IsAdmin
            ? dbContext.Campaigns
            : dbContext.Campaigns.Where(c => c.Members.Any(m => m.UserId == user.Id));

        var rows = await Summaries(campaigns.OrderByDescending(c => c.CreatedAt), user.Id).ToListAsync(cancellationToken);
        return [.. rows.Select(row => ToDto(row, user))];
    }

    public async Task<CampaignDetailsDto> GetAsync(Guid campaignId, CancellationToken cancellationToken)
    {
        await access.ForCampaignAsync(campaignId, cancellationToken).Demand(Operation.Read);
        var user = await RequireUserAsync(cancellationToken);
        var row = await Summaries(dbContext.Campaigns.Where(c => c.Id == campaignId), user.Id).SingleAsync(cancellationToken);
        var campaign = ToDto(row, user);

        var members = await dbContext.CampaignMembers
            .Where(m => m.CampaignId == campaignId)
            .OrderByDescending(m => m.Role == CampaignRole.Keeper)
            .ThenBy(m => m.JoinedAt)
            .Select(m => new
            {
                m.UserId,
                m.Role,
                m.DisplayName,
                m.JoinedAt,
                UserName = dbContext.Users.Where(u => u.Id == m.UserId).Select(u => u.DisplayName).First(),
            })
            .ToListAsync(cancellationToken);

        var campaignAccess = new Access.Access(CanRead: true, campaign.CanEdit, campaign.CanDelete);
        List<CampaignMemberDto> dtos =
        [
            .. members.Select(m =>
            {
                var memberAccess = AccessPolicy.ForMember(user, campaignAccess, m.UserId, m.Role);
                return new CampaignMemberDto(m.UserId, PublicNames.Of(m.DisplayName, m.UserName), m.DisplayName, m.Role, m.JoinedAt,
                    IsMe: m.UserId == user.Id, memberAccess.CanEdit, memberAccess.CanDelete);
            }),
        ];

        return new CampaignDetailsDto(campaign, dtos, CanLeave: campaign.MyRole is CampaignRole.Player);
    }

    /// <summary>Только Хранитель по роли (в v1 — кто угодно); создатель — Хранитель-участник кампании.</summary>
    public async Task<CampaignSummaryDto> CreateAsync(CampaignInput input, CancellationToken cancellationToken)
    {
        await access.CanCreateCampaignAsync(cancellationToken).Demand();
        var user = await RequireUserAsync(cancellationToken);
        var name = Validate(input);

        var campaign = new Campaign
        {
            Name = name,
            Kind = input.Kind,
            Status = input.Status,
            Era = input.Era,
            CreatedById = user.Id,
        };
        campaign.Members.Add(new CampaignMember { UserId = user.Id, Role = CampaignRole.Keeper });
        dbContext.Campaigns.Add(campaign);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Кампания {CampaignId} создана пользователем {UserId}", campaign.Id, user.Id);
        return await SummaryAsync(campaign.Id, user, cancellationToken);
    }

    public async Task<CampaignSummaryDto> UpdateAsync(Guid campaignId, CampaignInput input, CancellationToken cancellationToken)
    {
        await access.ForCampaignAsync(campaignId, cancellationToken).Demand(Operation.Edit);
        var user = await RequireUserAsync(cancellationToken);
        var name = Validate(input);

        var campaign = await dbContext.Campaigns.SingleAsync(c => c.Id == campaignId, cancellationToken);
        campaign.Name = name;
        campaign.Kind = input.Kind;
        campaign.Status = input.Status;
        campaign.Era = input.Era;
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Кампания {CampaignId} изменена пользователем {UserId}", campaignId, user.Id);
        return await SummaryAsync(campaignId, user, cancellationToken);
    }

    /// <summary>
    /// Удаляет кампанию с участниками, журналом и прохождениями (каскад в базе). Листы игроков остаются у
    /// них без кампании, НПС кампании возвращаются в библиотеку — это держат FK, а не код.
    /// </summary>
    public async Task DeleteAsync(Guid campaignId, CancellationToken cancellationToken)
    {
        await access.ForCampaignAsync(campaignId, cancellationToken).Demand(Operation.Delete);
        var user = await RequireUserAsync(cancellationToken);
        await dbContext.Campaigns.Where(c => c.Id == campaignId).ExecuteDeleteAsync(cancellationToken);
        logger.LogInformation("Кампания {CampaignId} удалена пользователем {UserId}", campaignId, user.Id);
    }

    /// <summary>
    /// Вступить: любой вошедший в незавершённую кампанию, где его ещё нет. Псевдоним сохраняется, только если
    /// отличается от имени профиля, — иначе участник идёт за именем из профиля.
    /// </summary>
    public async Task<CampaignDetailsDto> JoinAsync(Guid campaignId, JoinCampaignRequest request, CancellationToken cancellationToken)
    {
        await access.CanJoinCampaignAsync(campaignId, cancellationToken).Demand();
        var user = await RequireUserAsync(cancellationToken);

        dbContext.CampaignMembers.Add(new CampaignMember
        {
            CampaignId = campaignId,
            UserId = user.Id,
            Role = CampaignRole.Player,
            DisplayName = ValidateAlias(request.DisplayName, user.DisplayName),
        });

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // Две вкладки вступили одновременно: проверка прошла у обеих, ключ пустил одну.
            throw CampaignRejectedException.Conflict("Вы уже участник этой кампании.");
        }

        logger.LogInformation("Пользователь {UserId} вступил в кампанию {CampaignId}", user.Id, campaignId);
        return await GetAsync(campaignId, cancellationToken);
    }

    /// <summary>Псевдоним участника: свой — всегда, чужой — тот, кто правит кампанию.</summary>
    public async Task<CampaignMemberDto> UpdateMemberAsync(Guid campaignId, Guid userId, UpdateMemberRequest request,
        CancellationToken cancellationToken)
    {
        await access.ForMemberAsync(campaignId, userId, cancellationToken).Demand(Operation.Edit);
        var profileName = await dbContext.Users.Where(u => u.Id == userId).Select(u => u.DisplayName).SingleAsync(cancellationToken);
        var alias = ValidateAlias(request.DisplayName, profileName);

        await dbContext.CampaignMembers
            .Where(m => m.CampaignId == campaignId && m.UserId == userId)
            .ExecuteUpdateAsync(s => s.SetProperty(m => m.DisplayName, alias), cancellationToken);

        var details = await GetAsync(campaignId, cancellationToken);
        return details.Members.Single(m => m.UserId == userId);
    }

    /// <summary>
    /// Выйти самому или исключить игрока (Хранитель кампании). Хранителя убрать нельзя. Лист игрока при этом
    /// не пропадает — остаётся у него без кампании (<c>ON DELETE SET NULL (campaign_id)</c> составного FK).
    /// </summary>
    public async Task RemoveMemberAsync(Guid campaignId, Guid userId, CancellationToken cancellationToken)
    {
        var member = await access.ForMemberAsync(campaignId, userId, cancellationToken).Demand(Operation.Read);
        // Тому, кто мог бы убрать участника (сам он или Хранитель кампании), объясняем, почему нельзя;
        // остальным — обычный отказ по правам.
        if (member is { CanEdit: true, CanDelete: false } && await dbContext.CampaignMembers.AnyAsync(
                m => m.CampaignId == campaignId && m.UserId == userId && m.Role == CampaignRole.Keeper, cancellationToken))
        {
            throw CampaignRejectedException.Conflict("Хранителя из кампании не убрать: без него кампания не живёт.");
        }

        member.Demand(Operation.Delete);
        var user = await RequireUserAsync(cancellationToken);
        await dbContext.CampaignMembers
            .Where(m => m.CampaignId == campaignId && m.UserId == userId)
            .ExecuteDeleteAsync(cancellationToken);
        logger.LogInformation("Участник {MemberId} убран из кампании {CampaignId} пользователем {UserId}", userId, campaignId, user.Id);
    }

    private sealed record SummaryRow(
        Guid Id,
        string Name,
        CampaignKind Kind,
        CampaignStatus Status,
        Era Era,
        DateTimeOffset CreatedAt,
        CampaignRole? MyRole,
        string? KeeperAlias,
        string? KeeperName,
        int PlayerCount);

    private IQueryable<SummaryRow> Summaries(IQueryable<Campaign> campaigns, Guid userId) =>
        campaigns.Select(c => new SummaryRow(
            c.Id,
            c.Name,
            c.Kind,
            c.Status,
            c.Era,
            c.CreatedAt,
            c.Members.Where(m => m.UserId == userId).Select(m => (CampaignRole?)m.Role).FirstOrDefault(),
            c.Members.Where(m => m.Role == CampaignRole.Keeper).Select(m => m.DisplayName).FirstOrDefault(),
            c.Members.Where(m => m.Role == CampaignRole.Keeper)
                .Select(m => dbContext.Users.Where(u => u.Id == m.UserId).Select(u => u.DisplayName).FirstOrDefault())
                .FirstOrDefault(),
            c.Members.Count(m => m.Role == CampaignRole.Player)));

    private static CampaignSummaryDto ToDto(SummaryRow row, SignedInUser user)
    {
        var rights = AccessPolicy.ForCampaign(user, row.MyRole);
        return new CampaignSummaryDto(row.Id, row.Name, row.Kind, row.Status, row.Era, row.CreatedAt, row.MyRole,
            PublicNames.Of(row.KeeperAlias, row.KeeperName), row.PlayerCount, rights.CanEdit, rights.CanDelete);
    }

    private async Task<CampaignSummaryDto> SummaryAsync(Guid campaignId, SignedInUser user, CancellationToken cancellationToken) =>
        ToDto(await Summaries(dbContext.Campaigns.Where(c => c.Id == campaignId), user.Id).SingleAsync(cancellationToken), user);

    private async Task<SignedInUser> RequireUserAsync(CancellationToken cancellationToken) =>
        await currentUser.GetAsync(cancellationToken) ?? throw AccessDeniedException.NotFound();

    private static string Validate(CampaignInput input)
    {
        var name = input.Name?.Trim();
        if (string.IsNullOrEmpty(name))
        {
            throw new CampaignRejectedException("Название кампании не может быть пустым.");
        }

        if (name.Length > CampaignLimits.NameLength)
        {
            throw new CampaignRejectedException($"Название кампании — не длиннее {CampaignLimits.NameLength} символов.");
        }

        if (!Enum.IsDefined(input.Kind) || !Enum.IsDefined(input.Status) || !Enum.IsDefined(input.Era))
        {
            throw new CampaignRejectedException("Неизвестный вид, статус или эпоха кампании.");
        }

        return name;
    }

    private static string? ValidateAlias(string? requested, string profileName)
    {
        var alias = PublicNames.Alias(requested, profileName);
        if (alias is null)
        {
            return null;
        }

        if (alias.Length > CampaignLimits.DisplayNameLength)
        {
            throw new CampaignRejectedException($"Имя в кампании — не длиннее {CampaignLimits.DisplayNameLength} символов.");
        }

        if (alias.Contains('@', StringComparison.Ordinal))
        {
            // Почту другим участникам не показываем, а имя с «@» и не покажем вовсе.
            throw new CampaignRejectedException("Имя в кампании не должно быть почтой.");
        }

        return alias;
    }
}
