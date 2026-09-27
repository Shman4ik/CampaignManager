using CampaignManager.Web.Components.Features.Campaigns.Models;
using CampaignManager.Web.Components.Features.Characters.Model;
using CampaignManager.Web.Components.Features.Scenarios.Model;
using CampaignManager.Web.Utilities;
using CampaignManager.Web.Utilities.DataBase;
using CampaignManager.Web.Utilities.Services;
using Microsoft.EntityFrameworkCore;

namespace CampaignManager.Web.Components.Features.Campaigns.Services;

/// <summary>
///     Журнал встреч кампании. Права проверяются здесь, а не на странице:
///     <list type="bullet">
///         <item><description>вести журнал — Хранитель этой кампании (<c>Campaign.KeeperEmail</c>) и администратор;</description></item>
///         <item><description>читать — они же и игроки кампании (есть строка <c>CampaignPlayers</c>);</description></item>
///         <item><description>остальным журнал не отдаётся вовсе — <see cref="GetJournalAsync" /> вернёт <c>null</c>.</description></item>
///     </list>
///     Заметки Хранителя игрокам не отдаются: их обнуляет этот сервис, а не разметка страницы.
/// </summary>
public sealed class CampaignJournalService(
    IDbContextFactory<AppDbContext> dbContextFactory,
    IdentityService identityService,
    ILogger<CampaignJournalService> logger)
{
    private enum JournalAccess
    {
        None,
        Read,
        Write
    }

    private sealed record AccessInfo(Campaign? Campaign, JournalAccess Access, string? Email);

    /// <summary>
    ///     Журнал кампании для текущего пользователя, новые встречи сверху. <c>null</c> — кампании
    ///     нет или доступа к ней нет: вызывающий не должен различать эти случаи.
    /// </summary>
    public async Task<CampaignJournal?> GetJournalAsync(Guid campaignId)
    {
        try
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync();
            var (campaign, access, email) = await ResolveAccessAsync(dbContext, campaignId);

            if (campaign is null || access is JournalAccess.None)
            {
                logger.LogWarning("Denied journal read for campaign {CampaignId}", campaignId);
                return null;
            }

            var canEdit = access is JournalAccess.Write;

            var sessions = await dbContext.CampaignSessions
                .AsNoTracking()
                .Where(s => s.CampaignId == campaignId)
                .OrderByDescending(s => s.SessionDate)
                .ThenByDescending(s => s.Number)
                .ThenByDescending(s => s.CreatedAt)
                .Select(s => new CampaignSessionView(
                    s.Id,
                    s.Number,
                    s.SessionDate,
                    s.Title,
                    s.Summary,
                    s.KeeperNotes,
                    s.ScenarioId,
                    s.Scenario != null ? s.Scenario.Name : null,
                    s.ScenarioCompleted))
                .ToListAsync();

            // Заметки Хранителя не покидают сервис, если читатель не может их править.
            if (!canEdit)
                sessions = sessions.Select(s => s with { KeeperNotes = null }).ToList();

            var nextNumber = sessions.Count == 0 ? 1 : sessions.Max(s => s.Number) + 1;

            var scenarios = canEdit
                ? await LoadScenarioOptionsAsync(dbContext, campaign)
                : [];

            var investigators = await LoadInvestigatorsAsync(dbContext, campaignId, canEdit ? null : email);

            return new CampaignJournal(
                campaign.Id,
                campaign.Name,
                canEdit,
                nextNumber,
                sessions,
                scenarios,
                investigators);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error loading journal for campaign {CampaignId}", campaignId);
            return null;
        }
    }

    /// <summary>
    ///     Добавляет встречу (<see cref="CampaignSessionInput.Id" /> пуст) или правит существующую.
    /// </summary>
    public async Task<Result<Guid>> SaveSessionAsync(Guid campaignId, CampaignSessionInput input)
    {
        try
        {
            var title = Normalize(input.Title);
            var summary = Normalize(input.Summary);
            var keeperNotes = Normalize(input.KeeperNotes);

            if (input.Number is < 1 or > 9999)
                return Result<Guid>.Fail("Номер встречи — от 1 до 9999");
            if (title?.Length > CampaignJournalLimits.TitleLength)
                return Result<Guid>.Fail("Заголовок — не длиннее 200 символов");
            if (summary?.Length > CampaignJournalLimits.TextLength || keeperNotes?.Length > CampaignJournalLimits.TextLength)
                return Result<Guid>.Fail("Текст записи слишком длинный");

            await using var dbContext = await dbContextFactory.CreateDbContextAsync();
            var (campaign, access, email) = await ResolveAccessAsync(dbContext, campaignId);

            if (campaign is null)
                return Result<Guid>.Fail("Кампания не найдена");

            if (access is not JournalAccess.Write)
            {
                logger.LogWarning("Denied journal write for campaign {CampaignId} by {UserEmail}", campaignId, email);
                return Result<Guid>.Fail("Вести журнал может только Хранитель этой кампании");
            }

            CampaignSession session;
            if (input.Id is { } sessionId)
            {
                var existing = await dbContext.CampaignSessions
                    .FirstOrDefaultAsync(s => s.Id == sessionId && s.CampaignId == campaignId);

                if (existing is null)
                    return Result<Guid>.Fail("Запись журнала не найдена");

                session = existing;
            }
            else
            {
                session = new CampaignSession { CampaignId = campaignId };
                session.Init();
                dbContext.CampaignSessions.Add(session);
            }

            // Уже привязанный сценарий не перепроверяем: его могли с тех пор отвязать от кампании,
            // и правка заголовка не должна из-за этого падать.
            if (input.ScenarioId is { } scenarioId
                && scenarioId != session.ScenarioId
                && !await IsScenarioAllowedAsync(dbContext, campaign, scenarioId))
            {
                return Result<Guid>.Fail("Этот сценарий нельзя привязать к кампании");
            }

            session.SessionDate = input.SessionDate;
            session.Number = input.Number;
            session.Title = title;
            session.Summary = summary;
            session.KeeperNotes = keeperNotes;
            session.ScenarioId = input.ScenarioId;
            session.ScenarioCompleted = input.ScenarioCompleted;
            session.LastUpdated = DateTimeOffset.UtcNow;

            await dbContext.SaveChangesAsync();

            logger.LogInformation("Journal session {SessionId} of campaign {CampaignId} saved by {UserEmail}",
                session.Id, campaignId, email);
            return Result<Guid>.Ok(session.Id);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error saving journal session for campaign {CampaignId}", campaignId);
            return Result<Guid>.Fail("Не удалось сохранить запись журнала");
        }
    }

    /// <summary>Удаляет встречу. Право проверяется по кампании самой записи.</summary>
    public async Task<Result> DeleteSessionAsync(Guid sessionId)
    {
        try
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync();
            var session = await dbContext.CampaignSessions.FirstOrDefaultAsync(s => s.Id == sessionId);

            if (session is null)
                return Result.Fail("Запись журнала не найдена");

            var (_, access, email) = await ResolveAccessAsync(dbContext, session.CampaignId);
            if (access is not JournalAccess.Write)
            {
                logger.LogWarning("Denied journal delete of {SessionId} by {UserEmail}", sessionId, email);
                return Result.Fail("Вести журнал может только Хранитель этой кампании");
            }

            dbContext.CampaignSessions.Remove(session);
            await dbContext.SaveChangesAsync();

            logger.LogInformation("Journal session {SessionId} deleted by {UserEmail}", sessionId, email);
            return Result.Ok();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deleting journal session {SessionId}", sessionId);
            return Result.Fail("Не удалось удалить запись журнала");
        }
    }

    // ── Права ──────────────────────────────────────────────────────

    private async Task<AccessInfo> ResolveAccessAsync(AppDbContext dbContext, Guid campaignId)
    {
        // Асинхронный вариант обязателен: синхронный читает HttpContext и в интерактивном
        // рендере возвращает null (см. Characters/CLAUDE.md, «Authorization»).
        var email = await identityService.GetCurrentUserEmailAsync();
        if (string.IsNullOrEmpty(email))
            return new AccessInfo(null, JournalAccess.None, null);

        var campaign = await dbContext.Campaigns
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == campaignId);

        if (campaign is null)
            return new AccessInfo(null, JournalAccess.None, email);

        if (string.Equals(campaign.KeeperEmail, email, StringComparison.OrdinalIgnoreCase)
            || await identityService.IsAdministrator())
            return new AccessInfo(campaign, JournalAccess.Write, email);

        var emailLower = email.ToLower();
        var isPlayer = await dbContext.CampaignPlayers
            .AnyAsync(p => p.CampaignId == campaignId && p.PlayerEmail.ToLower() == emailLower);

        return new AccessInfo(campaign, isPlayer ? JournalAccess.Read : JournalAccess.None, email);
    }

    // ── Справочники формы ──────────────────────────────────────────

    /// <summary>
    ///     Сценарии, которые можно привязать к встрече: сценарии самой кампании и все сценарии её
    ///     Хранителя — ваншот или шаблон часто играют, так и не «добавив в кампанию».
    /// </summary>
    private static IQueryable<Scenario> AllowedScenarios(AppDbContext dbContext, Campaign campaign)
    {
        var keeperEmail = (campaign.KeeperEmail ?? string.Empty).ToLower();
        return dbContext.Scenarios.Where(s =>
            s.CampaignId == campaign.Id
            || (keeperEmail != string.Empty && s.CreatorEmail != null && s.CreatorEmail.ToLower() == keeperEmail));
    }

    private static async Task<List<JournalScenarioOption>> LoadScenarioOptionsAsync(AppDbContext dbContext, Campaign campaign)
    {
        return await AllowedScenarios(dbContext, campaign)
            .AsNoTracking()
            .OrderByDescending(s => s.CampaignId == campaign.Id)
            .ThenBy(s => s.Name)
            .Select(s => new JournalScenarioOption(s.Id, s.Name))
            .ToListAsync();
    }

    private static Task<bool> IsScenarioAllowedAsync(AppDbContext dbContext, Campaign campaign, Guid scenarioId) =>
        AllowedScenarios(dbContext, campaign).AnyAsync(s => s.Id == scenarioId);

    /// <summary>
    ///     Активные листы игроков кампании — ссылки для фазы развития. Хранитель видит всех, игрок —
    ///     только свой лист (<paramref name="onlyPlayerEmail" />): чужие ему всё равно не откроются.
    /// </summary>
    private static async Task<List<JournalInvestigator>> LoadInvestigatorsAsync(
        AppDbContext dbContext, Guid campaignId, string? onlyPlayerEmail)
    {
        var query = dbContext.CharacterStorage
            .AsNoTracking()
            .Where(c => c.Kind != CharacterKind.Npc
                        && c.Status == CharacterStatus.Active
                        && c.CampaignPlayer != null
                        && c.CampaignPlayer.CampaignId == campaignId);

        if (onlyPlayerEmail is not null)
        {
            var emailLower = onlyPlayerEmail.ToLower();
            query = query.Where(c => c.CampaignPlayer!.PlayerEmail.ToLower() == emailLower);
        }

        return await query
            .OrderBy(c => c.CharacterName)
            .Select(c => new JournalInvestigator(c.Id, c.CharacterName, c.CampaignPlayer!.PlayerName))
            .ToListAsync();
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
