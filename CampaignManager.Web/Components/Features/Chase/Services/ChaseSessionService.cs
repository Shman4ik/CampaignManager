using CampaignManager.Web.Components.Features.Chase.Model;
using CampaignManager.Web.Utilities.DataBase;
using CampaignManager.Web.Utilities.Services;
using Microsoft.EntityFrameworkCore;

namespace CampaignManager.Web.Components.Features.Chase.Services;

/// <summary>
/// Хранение сцены погони между подключениями. В отличие от <see cref="ChaseService"/> это обычный
/// сервис доступа к данным и следует шаблону из корневого CLAUDE.md.
/// </summary>
public sealed class ChaseSessionService(
    IDbContextFactory<AppDbContext> dbContextFactory,
    IdentityService identityService,
    ILogger<ChaseSessionService> logger)
{
    /// <summary>Сохранить (или перезаписать) текущую сцену Хранителя для выбранной кампании.</summary>
    public async Task SaveAsync(Guid? campaignId, ChaseSnapshot snapshot)
    {
        var keeperEmail = await identityService.GetCurrentUserEmailAsync();
        if (string.IsNullOrWhiteSpace(keeperEmail)) return;

        try
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync();

            var existing = await dbContext.ChaseSessions
                .FirstOrDefaultAsync(s => s.KeeperEmail == keeperEmail && s.CampaignId == campaignId);

            if (existing is null)
            {
                var session = new ChaseSessionDto
                {
                    CampaignId = campaignId,
                    KeeperEmail = keeperEmail,
                    State = snapshot
                };
                session.Init();
                dbContext.ChaseSessions.Add(session);
            }
            else
            {
                existing.State = snapshot;
                existing.LastUpdated = DateTimeOffset.UtcNow;
            }

            await dbContext.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Не удалось сохранить сцену погони для {KeeperEmail} / {CampaignId}",
                keeperEmail, campaignId);
        }
    }

    /// <summary>Загрузить сцену для конкретной кампании.</summary>
    public async Task<ChaseSnapshot?> LoadAsync(Guid? campaignId)
    {
        var keeperEmail = await identityService.GetCurrentUserEmailAsync();
        if (string.IsNullOrWhiteSpace(keeperEmail)) return null;

        try
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync();
            var session = await dbContext.ChaseSessions
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.KeeperEmail == keeperEmail && s.CampaignId == campaignId);

            return session?.State;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Не удалось загрузить сцену погони для {KeeperEmail} / {CampaignId}",
                keeperEmail, campaignId);
            return null;
        }
    }

    /// <summary>
    /// Последняя сцена Хранителя независимо от кампании — этим восстанавливается погоня
    /// после обрыва соединения, когда кампания ещё не выбрана заново.
    /// </summary>
    public async Task<(ChaseSnapshot Snapshot, Guid? CampaignId)?> LoadLatestAsync()
    {
        var keeperEmail = await identityService.GetCurrentUserEmailAsync();
        if (string.IsNullOrWhiteSpace(keeperEmail)) return null;

        try
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync();
            var session = await dbContext.ChaseSessions
                .AsNoTracking()
                .Where(s => s.KeeperEmail == keeperEmail)
                .OrderByDescending(s => s.LastUpdated)
                .FirstOrDefaultAsync();

            return session is null ? null : (session.State, session.CampaignId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Не удалось загрузить последнюю сцену погони для {KeeperEmail}", keeperEmail);
            return null;
        }
    }

    public async Task DeleteAsync(Guid? campaignId)
    {
        var keeperEmail = await identityService.GetCurrentUserEmailAsync();
        if (string.IsNullOrWhiteSpace(keeperEmail)) return;

        try
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync();
            await dbContext.ChaseSessions
                .Where(s => s.KeeperEmail == keeperEmail && s.CampaignId == campaignId)
                .ExecuteDeleteAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Не удалось удалить сцену погони для {KeeperEmail} / {CampaignId}",
                keeperEmail, campaignId);
        }
    }
}
