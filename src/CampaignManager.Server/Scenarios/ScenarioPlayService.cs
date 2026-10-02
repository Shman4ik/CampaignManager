using CampaignManager.Contracts.Scenarios;
using CampaignManager.Core.Campaigns;
using CampaignManager.Core.Music;
using CampaignManager.Data;
using CampaignManager.Data.Scenarios;
using CampaignManager.Server.Access;
using CampaignManager.Server.Platform;
using Microsoft.EntityFrameworkCore;

namespace CampaignManager.Server.Scenarios;

/// <summary>
/// Режим игры (T2.5b): прохождения, из которых берутся сыщики для проверок, музыка локации и раздатка для показа
/// игрокам (второй экран). Знание модуля — <c>Scenarios/CLAUDE.md</c>.
/// </summary>
public sealed class ScenarioPlayService(
    CmDbContext dbContext,
    AccessPolicy access,
    CurrentUser currentUser,
    ILogger<ScenarioPlayService> logger)
{
    /// <summary>
    /// Прохождения сценария в кампаниях, которые ведёт вошедший (администратору — все): только там ему отдадут сыщиков
    /// (<c>GET /campaigns/{id}/investigators</c> — Хранителю кампании). Незавершённые — первыми, свежие — выше.
    /// Создание прохождений и анонс — T2.5c.
    /// </summary>
    public async Task<IReadOnlyList<ScenarioRunDto>> ListRunsAsync(Guid scenarioId, CancellationToken cancellationToken)
    {
        await access.ForScenarioAsync(scenarioId, cancellationToken).Demand(Operation.Read);
        var user = await currentUser.GetAsync(cancellationToken) ?? throw AccessDeniedException.Forbidden();

        var rows = await dbContext.ScenarioRuns.AsNoTracking()
            .Where(r => r.ScenarioId == scenarioId)
            .Select(r => new
            {
                r.Id,
                r.CampaignId,
                CampaignName = dbContext.Campaigns.Where(c => c.Id == r.CampaignId).Select(c => c.Name).First(),
                r.Status,
                r.ScheduledAt,
                r.CreatedAt,
                Role = dbContext.CampaignMembers.Where(m => m.CampaignId == r.CampaignId && m.UserId == user.Id)
                    .Select(m => (CampaignRole?)m.Role).FirstOrDefault(),
            })
            .ToListAsync(cancellationToken);

        return
        [
            .. rows
                .Where(r => AccessPolicy.ForCampaign(user, r.Role).CanEdit)
                .OrderBy(r => r.Status == ScenarioRunStatus.Finished)
                .ThenByDescending(r => r.ScheduledAt ?? r.CreatedAt)
                .Select(r => new ScenarioRunDto(r.Id, r.CampaignId, r.CampaignName, r.Status, r.ScheduledAt)),
        ];
    }

    /// <summary>
    /// Музыка локации — оба списка целиком: настроения нормализуются (как теги фонотеки — «Бой» и «бой» один пул), прибитые
    /// треки — только из фонотеки. Трек, удалённый из фонотеки, уходит из локации сам (каскад <c>location_tracks</c>).
    /// </summary>
    public async Task<ScenarioLocationDto> SetLocationMusicAsync(Guid scenarioId, Guid locationId, LocationMusicInput input,
        CancellationToken cancellationToken)
    {
        await access.ForScenarioAsync(scenarioId, cancellationToken).Demand(Operation.Edit);
        var location = await dbContext.ScenarioLocations
                           .Include(l => l.Checks)
                           .SingleOrDefaultAsync(l => l.Id == locationId && l.ScenarioId == scenarioId, cancellationToken)
                       ?? throw AccessDeniedException.NotFound();

        var tags = MusicTags.Normalize(input.Tags ?? []);
        if (tags.Count > MusicTags.MaxCount)
        {
            throw ApiProblemException.Invalid($"Настроений у локации — не больше {MusicTags.MaxCount}.");
        }

        if (tags.FirstOrDefault(t => t.Length > MusicTags.MaxLength) is { } longTag)
        {
            throw ApiProblemException.Invalid($"Настроение «{longTag}» длиннее {MusicTags.MaxLength} знаков.");
        }

        var trackIds = (input.TrackIds ?? []).Distinct().ToList();
        if (trackIds.Count > MusicTags.MaxCount)
        {
            throw ApiProblemException.Invalid($"Прибитых треков у локации — не больше {MusicTags.MaxCount}.");
        }

        var known = await dbContext.MusicTracks.CountAsync(t => trackIds.Contains(t.Id), cancellationToken);
        if (known != trackIds.Count)
        {
            throw ApiProblemException.Invalid("Такого трека нет в фонотеке — перечитайте её.");
        }

        location.MusicTags = tags;
        var current = await dbContext.LocationTracks.Where(t => t.LocationId == locationId).ToListAsync(cancellationToken);
        dbContext.LocationTracks.RemoveRange(current.Where(t => !trackIds.Contains(t.TrackId)));
        dbContext.LocationTracks.AddRange(trackIds.Where(id => current.All(t => t.TrackId != id))
            .Select(id => new LocationTrack { LocationId = locationId, TrackId = id }));
        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Музыка локации {LocationId}: {TagCount} настроений, {TrackCount} треков", locationId, tags.Count, trackIds.Count);

        var tracks = await dbContext.LocationTracks.AsNoTracking().Where(t => t.LocationId == locationId).ToListAsync(cancellationToken);
        var skillIds = location.Checks.Select(c => c.SkillId).OfType<Guid>().ToList();
        var skills = await dbContext.Skills.AsNoTracking().Where(k => skillIds.Contains(k.Id))
            .ToDictionaryAsync(k => k.Id, k => k.Name, cancellationToken);
        return ScenarioParts.Location(location, tracks, skills);
    }

    /// <summary>
    /// Раздатка для показа игрокам и второго экрана: Хранитель или игрок кампании, где сценарий проходят
    /// (<see cref="AccessPolicy.ForHandoutAsync"/>). Раздатка чужого сценария по адресу этого — 404, как и несуществующая.
    /// Пометки и заметки Хранителя в ответе нет вовсе (<see cref="HandoutScreenDto"/>).
    /// </summary>
    public async Task<HandoutScreenDto> GetHandoutScreenAsync(Guid scenarioId, Guid handoutId, CancellationToken cancellationToken)
    {
        var rights = await access.ForHandoutAsync(handoutId, cancellationToken).Demand(Operation.Read);
        var handout = await dbContext.ScenarioHandouts.AsNoTracking()
                          .Where(h => h.Id == handoutId && h.ScenarioId == scenarioId)
                          .Select(h => new { h.Id, h.PlayerText, h.FileId })
                          .SingleOrDefaultAsync(cancellationToken)
                      ?? throw AccessDeniedException.NotFound();

        return new HandoutScreenDto(handout.Id, scenarioId, handout.PlayerText,
            handout.FileId is { } file ? Contracts.Files.FilesRoutes.Content(file) : null, rights.CanEdit);
    }
}
