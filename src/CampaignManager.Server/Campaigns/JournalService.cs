using CampaignManager.Contracts.Campaigns;
using CampaignManager.Core.Campaigns;
using CampaignManager.Core.Characters;
using CampaignManager.Data;
using CampaignManager.Data.Campaigns;
using CampaignManager.Server.Access;
using Microsoft.EntityFrameworkCore;

namespace CampaignManager.Server.Campaigns;

/// <summary>
/// Журнал встреч кампании. Читают участники, пишет и удаляет тот, кто правит кампанию (её Хранитель или
/// администратор). Заметки Хранителя вырезает этот сервис, а не разметка: читателю без права правки они не
/// уходят вовсе. Нет кампании и нет доступа — одинаково 404.
/// </summary>
public sealed class JournalService(
    CmDbContext dbContext,
    AccessPolicy access,
    CurrentUser currentUser,
    ILogger<JournalService> logger)
{
    public async Task<CampaignJournalDto> GetAsync(Guid campaignId, CancellationToken cancellationToken)
    {
        var rights = await access.ForCampaignAsync(campaignId, cancellationToken).Demand(Operation.Read);
        var user = await currentUser.GetAsync(cancellationToken) ?? throw AccessDeniedException.NotFound();
        var canEdit = rights.CanEdit;

        var campaignName = await dbContext.Campaigns.Where(c => c.Id == campaignId).Select(c => c.Name).SingleAsync(cancellationToken);

        // Новые сверху: по дате встречи, затем по номеру. Журнал маленький — сортируется здесь, а не в
        // SQL: проекция в record-конструктор не даёт EF сортировать по её полям.
        List<CampaignSessionDto> sessions =
        [
            .. (await SessionViews(dbContext.CampaignSessions.Where(s => s.CampaignId == campaignId)).ToListAsync(cancellationToken))
            .OrderByDescending(s => s.SessionDate)
            .ThenByDescending(s => s.Number)
            .ThenByDescending(s => s.Id),
        ];

        // Заметки Хранителя не покидают сервер, если читатель не может их править.
        if (!canEdit)
        {
            sessions = [.. sessions.Select(s => s with { KeeperNotes = null })];
        }

        var nextNumber = sessions.Count == 0 ? 1 : Math.Min(sessions.Max(s => s.Number) + 1, CampaignLimits.MaxSessionNumber);

        List<JournalRunOption> runs = canEdit
            ? await dbContext.ScenarioRuns
                .Where(r => r.CampaignId == campaignId)
                .OrderByDescending(r => r.ScheduledAt ?? r.CreatedAt)
                .Select(r => new JournalRunOption(
                    r.Id,
                    r.ScenarioId,
                    dbContext.Scenarios.Where(s => s.Id == r.ScenarioId).Select(s => s.Name).First(),
                    r.ScheduledAt))
                .ToListAsync(cancellationToken)
            : [];

        // Ссылки для фазы развития: Хранителю — все активные листы игроков кампании, игроку — свой
        // (чужой лист ему всё равно не откроется).
        var investigators = await dbContext.Characters
            .Where(c => c.Kind == CharacterKind.Player
                        && c.Status == CharacterStatus.Active
                        && c.CampaignId == campaignId
                        && (canEdit || c.OwnerId == user.Id))
            .OrderBy(c => c.Name)
            .Select(c => new
            {
                c.Id,
                c.Name,
                Member = dbContext.CampaignMembers
                    .Where(m => m.CampaignId == campaignId && m.UserId == c.OwnerId)
                    .Select(m => new
                    {
                        m.DisplayName,
                        UserName = dbContext.Users.Where(u => u.Id == m.UserId).Select(u => u.DisplayName).First(),
                    })
                    .FirstOrDefault(),
            })
            .ToListAsync(cancellationToken);

        return new CampaignJournalDto(
            campaignId,
            campaignName,
            canEdit,
            nextNumber,
            sessions,
            runs,
            [.. investigators.Select(c => new JournalInvestigator(c.Id, c.Name ?? "Без имени",
                PublicNames.Of(c.Member?.DisplayName, c.Member?.UserName)))]);
    }

    public async Task<CampaignSessionDto> AddAsync(Guid campaignId, CampaignSessionInput input, CancellationToken cancellationToken)
    {
        await access.ForCampaignAsync(campaignId, cancellationToken).Demand(Operation.Edit);
        var session = new CampaignSession { CampaignId = campaignId };
        await ApplyAsync(session, input, cancellationToken);
        dbContext.CampaignSessions.Add(session);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Встреча {SessionId} записана в журнал кампании {CampaignId}", session.Id, campaignId);
        return await ViewAsync(session.Id, cancellationToken);
    }

    public async Task<CampaignSessionDto> UpdateAsync(Guid campaignId, Guid sessionId, CampaignSessionInput input,
        CancellationToken cancellationToken)
    {
        await access.ForCampaignAsync(campaignId, cancellationToken).Demand(Operation.Edit);
        var session = await dbContext.CampaignSessions.SingleOrDefaultAsync(s => s.Id == sessionId && s.CampaignId == campaignId,
                          cancellationToken)
                      ?? throw AccessDeniedException.NotFound();
        await ApplyAsync(session, input, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Встреча {SessionId} кампании {CampaignId} изменена", sessionId, campaignId);
        return await ViewAsync(sessionId, cancellationToken);
    }

    public async Task DeleteAsync(Guid campaignId, Guid sessionId, CancellationToken cancellationToken)
    {
        await access.ForCampaignAsync(campaignId, cancellationToken).Demand(Operation.Delete);
        var deleted = await dbContext.CampaignSessions
            .Where(s => s.Id == sessionId && s.CampaignId == campaignId)
            .ExecuteDeleteAsync(cancellationToken);
        if (deleted == 0)
        {
            throw AccessDeniedException.NotFound();
        }

        logger.LogInformation("Встреча {SessionId} удалена из журнала кампании {CampaignId}", sessionId, campaignId);
    }

    private async Task ApplyAsync(CampaignSession session, CampaignSessionInput input, CancellationToken cancellationToken)
    {
        var title = Normalize(input.Title);
        var summary = Normalize(input.Summary);
        var keeperNotes = Normalize(input.KeeperNotes);

        if (input.Number is < CampaignLimits.MinSessionNumber or > CampaignLimits.MaxSessionNumber)
        {
            throw new CampaignRejectedException(
                $"Номер встречи — от {CampaignLimits.MinSessionNumber} до {CampaignLimits.MaxSessionNumber}.");
        }

        if (title?.Length > CampaignLimits.SessionTitleLength)
        {
            throw new CampaignRejectedException($"Заголовок — не длиннее {CampaignLimits.SessionTitleLength} символов.");
        }

        if (summary?.Length > CampaignLimits.SessionTextLength || keeperNotes?.Length > CampaignLimits.SessionTextLength)
        {
            throw new CampaignRejectedException("Текст записи слишком длинный.");
        }

        // Встреча — часть игры этой кампании: привязать можно только её прохождение. Уже привязанное не
        // перепроверяем — правка заголовка не должна падать из-за того, что прохождение с тех пор изменилось.
        if (input.RunId is { } runId && runId != session.RunId
                                     && !await dbContext.ScenarioRuns.AnyAsync(r => r.Id == runId && r.CampaignId == session.CampaignId,
                                         cancellationToken))
        {
            throw new CampaignRejectedException("Это прохождение не из этой кампании.");
        }

        session.SessionDate = input.SessionDate;
        session.Number = input.Number;
        session.Title = title;
        session.Summary = summary;
        session.KeeperNotes = keeperNotes;
        session.RunId = input.RunId;
        session.ScenarioCompleted = input.ScenarioCompleted;
    }

    private async Task<CampaignSessionDto> ViewAsync(Guid sessionId, CancellationToken cancellationToken) =>
        await SessionViews(dbContext.CampaignSessions.Where(s => s.Id == sessionId)).SingleAsync(cancellationToken);

    private IQueryable<CampaignSessionDto> SessionViews(IQueryable<CampaignSession> sessions) =>
        from s in sessions
        join r in dbContext.ScenarioRuns on s.RunId equals r.Id into runs
        from run in runs.DefaultIfEmpty()
        select new CampaignSessionDto(
            s.Id,
            s.Number,
            s.SessionDate,
            s.Title,
            s.Summary,
            s.KeeperNotes,
            s.RunId,
            run == null ? null : (Guid?)run.ScenarioId,
            run == null ? null : dbContext.Scenarios.Where(sc => sc.Id == run.ScenarioId).Select(sc => sc.Name).FirstOrDefault(),
            s.ScenarioCompleted);

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
