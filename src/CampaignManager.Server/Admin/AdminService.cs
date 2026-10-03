using CampaignManager.Contracts.Admin;
using CampaignManager.Contracts.Profile;
using CampaignManager.Core.Characters;
using CampaignManager.Core.Identity;
using CampaignManager.Data;
using CampaignManager.Server.Access;
using CampaignManager.Server.Platform;
using Microsoft.EntityFrameworkCore;

namespace CampaignManager.Server.Admin;

/// <summary>
/// Админка: пользователи, роли, заявки на Хранителя. Каждый метод — <see cref="AccessPolicy.CanAdministerAsync"/>
/// и <c>Demand</c>, хотя эндпоинты и так под политикой <c>Admin</c>: сервис — последняя линия, как и в v1
/// (<c>EnsureAdministratorAsync</c>). Знание модуля — <c>Admin/CLAUDE.md</c>.
/// </summary>
public sealed class AdminService(
    CmDbContext dbContext,
    AccessPolicy access,
    CurrentUser currentUser,
    TimeProvider timeProvider,
    ILogger<AdminService> logger)
{
    public async Task<AdminSummaryDto> GetSummaryAsync(CancellationToken cancellationToken)
    {
        await access.CanAdministerAsync(cancellationToken).Demand();
        var pending = await dbContext.KeeperApplications.CountAsync(a => a.Status == KeeperApplicationStatus.Pending, cancellationToken);
        return new AdminSummaryDto(pending);
    }

    /// <summary>Все пользователи по почте — их десятки, поиск и страницы делает клиент.</summary>
    public async Task<IReadOnlyList<AdminUserDto>> GetUsersAsync(CancellationToken cancellationToken)
    {
        var me = await DemandAdminAsync(cancellationToken);
        return await Users(dbContext.Users.OrderBy(u => u.Email), me.Id).ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Сменить роль. Роль читается из базы на каждый запрос (<see cref="CurrentUser"/>), поэтому новая видна со
    /// следующего запроса — в v1 после смены приходилось сбрасывать кэш claims на пять минут. Последнего
    /// администратора не понизить (409): иначе админку некому открыть, кроме <c>AdminEmails</c> при входе.
    /// </summary>
    public async Task<AdminUserDto> ChangeRoleAsync(Guid userId, UserRole role, CancellationToken cancellationToken)
    {
        var me = await DemandAdminAsync(cancellationToken);
        if (!Enum.IsDefined(role))
        {
            throw ApiProblemException.Invalid("Неизвестная роль.");
        }

        var user = await dbContext.Users.SingleOrDefaultAsync(u => u.Id == userId, cancellationToken)
                   ?? throw AccessDeniedException.NotFound();

        if (user.Role is UserRole.Admin && role is not UserRole.Admin
                                        && await dbContext.Users.CountAsync(u => u.Role == UserRole.Admin, cancellationToken) <= 1)
        {
            throw ApiProblemException.Conflict("Это последний администратор: сначала назначьте другого.");
        }

        if (user.Role != role)
        {
            var previous = user.Role;
            user.Role = role;
            await dbContext.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Роль пользователя {UserId}: {Previous} → {Role} (администратор {AdminId})", userId, previous, role, me.Id);
        }

        return await Users(dbContext.Users.Where(u => u.Id == userId), me.Id).SingleAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<KeeperApplicationDto>> GetApplicationsAsync(KeeperApplicationStatus? status,
        CancellationToken cancellationToken)
    {
        await DemandAdminAsync(cancellationToken);
        var query = dbContext.KeeperApplications.AsQueryable();
        if (status is { } filter)
        {
            query = query.Where(a => a.Status == filter);
        }

        return await Applications(query.OrderByDescending(a => a.CreatedAt)).ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Одобрить — <b>одной транзакцией</b>: статус заявки и роль. В v1 они писались в разные контексты (схемы
    /// <c>games</c> и <c>identity</c>), и сбой между ними оставлял одобренную заявку без роли. Роль только
    /// повышается: администратор, подавший когда-то заявку, Хранителем не становится.
    /// </summary>
    public async Task<KeeperApplicationDto> ApproveAsync(Guid applicationId, CancellationToken cancellationToken)
    {
        var me = await DemandAdminAsync(cancellationToken);
        var now = timeProvider.GetUtcNow();

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var userId = await ReviewAsync(applicationId, KeeperApplicationStatus.Approved, me.Id, now, comment: null, cancellationToken);
        await dbContext.Users
            .Where(u => u.Id == userId && u.Role == UserRole.Player)
            .ExecuteUpdateAsync(s => s
                .SetProperty(u => u.Role, UserRole.Keeper)
                .SetProperty(u => u.UpdatedAt, now), cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        logger.LogInformation("Заявка {ApplicationId} одобрена администратором {AdminId}: {UserId} — Хранитель", applicationId, me.Id, userId);
        return await Applications(dbContext.KeeperApplications.Where(a => a.Id == applicationId)).SingleAsync(cancellationToken);
    }

    /// <summary>Отклонить с комментарием — его увидит подавший в кабинете. Роль не меняется.</summary>
    public async Task<KeeperApplicationDto> RejectAsync(Guid applicationId, string? comment, CancellationToken cancellationToken)
    {
        var me = await DemandAdminAsync(cancellationToken);
        var text = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim();
        if (text?.Length > ProfileLimits.ReviewCommentLength)
        {
            throw ApiProblemException.Invalid($"Комментарий — не длиннее {ProfileLimits.ReviewCommentLength} символов.");
        }

        await ReviewAsync(applicationId, KeeperApplicationStatus.Rejected, me.Id, timeProvider.GetUtcNow(), text, cancellationToken);
        logger.LogInformation("Заявка {ApplicationId} отклонена администратором {AdminId}", applicationId, me.Id);
        return await Applications(dbContext.KeeperApplications.Where(a => a.Id == applicationId)).SingleAsync(cancellationToken);
    }

    /// <summary>
    /// Закрывает заявку, только если она ещё на рассмотрении — условие в самом <c>UPDATE</c>: два администратора
    /// (или две вкладки) не рассмотрят её дважды. Нет заявки — 404, уже рассмотрена — 409.
    /// </summary>
    private async Task<Guid> ReviewAsync(Guid applicationId, KeeperApplicationStatus status, Guid reviewerId, DateTimeOffset now,
        string? comment, CancellationToken cancellationToken)
    {
        var updated = await dbContext.KeeperApplications
            .Where(a => a.Id == applicationId && a.Status == KeeperApplicationStatus.Pending)
            .ExecuteUpdateAsync(s => s
                .SetProperty(a => a.Status, status)
                .SetProperty(a => a.ReviewedById, reviewerId)
                .SetProperty(a => a.ReviewedAt, now)
                .SetProperty(a => a.ReviewComment, comment)
                .SetProperty(a => a.UpdatedAt, now), cancellationToken);

        var userId = await dbContext.KeeperApplications.Where(a => a.Id == applicationId)
            .Select(a => (Guid?)a.UserId)
            .SingleOrDefaultAsync(cancellationToken) ?? throw AccessDeniedException.NotFound();

        return updated == 1 ? userId : throw ApiProblemException.Conflict("Заявка уже рассмотрена.");
    }

    // Проекция в record — последней: EF не переводит Where/OrderBy по полям, собранным конструктором.
    private IQueryable<AdminUserDto> Users(IQueryable<Data.Identity.User> users, Guid myId) =>
        users.Select(u => new AdminUserDto(
            u.Id,
            u.Email,
            u.DisplayName,
            u.Role,
            u.RegisteredAt,
            u.LastLoginAt,
            u.Id == myId,
            dbContext.KeeperApplications.Any(a => a.UserId == u.Id && a.Status == KeeperApplicationStatus.Pending),
            dbContext.CampaignMembers.Count(m => m.UserId == u.Id),
            dbContext.Characters.Count(c => c.OwnerId == u.Id && c.Kind == CharacterKind.Player && c.Status != CharacterStatus.Archived)));

    private IQueryable<KeeperApplicationDto> Applications(IQueryable<Data.Identity.KeeperApplication> query) =>
        from a in query
        join u in dbContext.Users on a.UserId equals u.Id
        select new KeeperApplicationDto(
            a.Id,
            a.UserId,
            u.DisplayName,
            u.Email,
            u.Role,
            a.Message,
            a.Status,
            a.CreatedAt,
            a.ReviewedAt,
            dbContext.Users.Where(r => r.Id == a.ReviewedById).Select(r => r.DisplayName).FirstOrDefault(),
            a.ReviewComment);

    private async Task<SignedInUser> DemandAdminAsync(CancellationToken cancellationToken)
    {
        await access.CanAdministerAsync(cancellationToken).Demand();
        return await currentUser.GetAsync(cancellationToken) ?? throw AccessDeniedException.Forbidden();
    }
}
