using CampaignManager.Contracts.Files;
using CampaignManager.Contracts.Scenarios;
using CampaignManager.Core;
using CampaignManager.Core.Campaigns;
using CampaignManager.Core.Characters;
using CampaignManager.Core.Documents;
using CampaignManager.Data;
using CampaignManager.Data.Scenarios;
using CampaignManager.Server.Access;
using CampaignManager.Server.Campaigns;
using CampaignManager.Server.Characters;
using CampaignManager.Server.Platform;
using Microsoft.EntityFrameworkCore;

namespace CampaignManager.Server.Scenarios;

/// <summary>
/// Сценарий целиком: библиотека, рабочее место одним запросом, шапка и текст (корень с версией), удаление и
/// порядок строк. Вложенные части пишет <see cref="ScenarioPartsService"/>, состав НПС и прегенов —
/// <see cref="ScenarioCastService"/>. Знание модуля — <c>Scenarios/CLAUDE.md</c>.
/// </summary>
public sealed class ScenarioService(
    CmDbContext dbContext,
    AccessPolicy access,
    CurrentUser currentUser,
    ILogger<ScenarioService> logger)
{
    /// <summary>Библиотека: все сценарии (их пять — проекция дешевле любого кэша), флаги — правилом по строке.</summary>
    public async Task<ScenarioListDto> ListAsync(CancellationToken cancellationToken)
    {
        await access.CanBrowseScenariosAsync(cancellationToken).Demand();
        var user = await currentUser.GetAsync(cancellationToken) ?? throw AccessDeniedException.Forbidden();

        var rows = await dbContext.Scenarios.AsNoTracking()
            .Select(s => new
            {
                s.Id,
                s.Name,
                s.Summary,
                s.Setting,
                s.Era,
                s.SettingDate,
                s.AuthorId,
                s.UpdatedAt,
                Author = dbContext.Users.Where(u => u.Id == s.AuthorId).Select(u => u.DisplayName).FirstOrDefault(),
                SourceName = dbContext.Scenarios.Where(x => x.Id == s.SourceScenarioId).Select(x => x.Name).FirstOrDefault(),
                Locations = s.Locations.Count,
                Handouts = s.Handouts.Count,
                Npcs = s.Npcs.Count,
                Pregens = dbContext.Characters.Count(c => c.ScenarioId == s.Id && c.Kind == CharacterKind.Pregen
                                                          && c.Status != CharacterStatus.Archived),
                Runs = dbContext.ScenarioRuns.Count(r => r.ScenarioId == s.Id),
            })
            .ToListAsync(cancellationToken);

        var items = rows
            .Select(r =>
            {
                var rights = AccessPolicy.ForScenario(user, r.AuthorId);
                return new ScenarioSummaryDto
                {
                    Id = r.Id,
                    Name = r.Name,
                    Summary = r.Summary,
                    Setting = r.Setting,
                    Era = r.Era,
                    SettingDate = r.SettingDate,
                    SourceScenarioName = r.SourceName,
                    AuthorName = PublicNames.Of(null, r.Author),
                    LocationCount = r.Locations,
                    HandoutCount = r.Handouts,
                    NpcCount = r.Npcs,
                    PregenCount = r.Pregens,
                    RunCount = r.Runs,
                    UpdatedAt = r.UpdatedAt,
                    CanEdit = rights.CanEdit,
                    CanDelete = rights.CanDelete,
                };
            })
            .OrderBy(s => s.Name, StringComparer.CurrentCulture)
            .ThenBy(s => s.SourceScenarioName is not null)
            .ToList();

        return new ScenarioListDto(items, await access.CanCreateScenarioAsync(cancellationToken));
    }

    /// <summary>Новый сценарий; автор — тот, кто завёл (ставит сервис, а не форма: в v1 импорт автора не ставил).</summary>
    public async Task<ScenarioDto> CreateAsync(ScenarioInput input, CancellationToken cancellationToken)
    {
        await access.CanCreateScenarioAsync(cancellationToken).Demand();
        var user = await currentUser.GetAsync(cancellationToken) ?? throw AccessDeniedException.Forbidden();

        var scenario = new Scenario { Name = "", AuthorId = user.Id };
        ApplyHeader(scenario, input);
        dbContext.Scenarios.Add(scenario);
        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Создан сценарий {ScenarioId}", scenario.Id);
        return await GetAsync(scenario.Id, cancellationToken);
    }

    /// <summary>Рабочее место: шапка, текст и все части одним запросом. Игроку сценарий не виден — 404.</summary>
    public async Task<ScenarioDto> GetAsync(Guid scenarioId, CancellationToken cancellationToken)
    {
        var rights = await access.ForScenarioAsync(scenarioId, cancellationToken).Demand(Operation.Read);
        var user = await currentUser.GetAsync(cancellationToken) ?? throw AccessDeniedException.Forbidden();

        var scenario = await dbContext.Scenarios.AsNoTracking()
            .Where(s => s.Id == scenarioId)
            .Select(s => new
            {
                Row = s,
                Author = dbContext.Users.Where(u => u.Id == s.AuthorId).Select(u => u.DisplayName).FirstOrDefault(),
                SourceName = dbContext.Scenarios.Where(x => x.Id == s.SourceScenarioId).Select(x => x.Name).FirstOrDefault(),
                Runs = dbContext.ScenarioRuns.Count(r => r.ScenarioId == s.Id),
            })
            .SingleAsync(cancellationToken);
        var s = scenario.Row;

        var locations = await dbContext.ScenarioLocations.AsNoTracking()
            .Where(l => l.ScenarioId == scenarioId)
            .Include(l => l.Checks)
            .ToListAsync(cancellationToken);
        var locationIds = locations.Select(l => l.Id).ToList();
        var tracks = await dbContext.LocationTracks.AsNoTracking()
            .Where(t => locationIds.Contains(t.LocationId))
            .ToListAsync(cancellationToken);
        var skillIds = locations.SelectMany(l => l.Checks).Select(c => c.SkillId).OfType<Guid>().Distinct().ToList();
        var skills = await dbContext.Skills.AsNoTracking()
            .Where(k => skillIds.Contains(k.Id))
            .ToDictionaryAsync(k => k.Id, k => k.Name, cancellationToken);

        var facts = await dbContext.ScenarioKeyFacts.AsNoTracking().Where(f => f.ScenarioId == scenarioId)
            .OrderBy(f => f.Ord).ToListAsync(cancellationToken);
        var handouts = await dbContext.ScenarioHandouts.AsNoTracking().Where(h => h.ScenarioId == scenarioId)
            .OrderBy(h => h.Ord).ToListAsync(cancellationToken);
        var creatures = await ScenarioParts.CreaturesAsync(dbContext, dbContext.ScenarioCreatures.Where(c => c.ScenarioId == scenarioId),
            cancellationToken);
        var items = await ScenarioParts.ItemsAsync(dbContext, dbContext.ScenarioItems.Where(i => i.ScenarioId == scenarioId),
            cancellationToken);

        var cast = await dbContext.ScenarioNpcs.AsNoTracking().Where(n => n.ScenarioId == scenarioId).ToListAsync(cancellationToken);
        var castIds = cast.Select(n => n.CharacterId).ToList();
        var npcSummaries = await CharacterSummaries.ReadAsync(dbContext, dbContext.Characters.Where(c => castIds.Contains(c.Id)), user,
            cancellationToken);
        var pregenSummaries = await CharacterSummaries.ReadAsync(dbContext,
            dbContext.Characters.Where(c => c.ScenarioId == scenarioId && c.Kind == CharacterKind.Pregen
                                                                       && c.Status != CharacterStatus.Archived),
            user, cancellationToken);
        var reserved = await ReservedPregensAsync(dbContext, scenarioId, cancellationToken);

        return new ScenarioDto
        {
            Id = s.Id,
            Version = s.Version,
            Name = s.Name,
            Summary = s.Summary,
            BodyMd = s.BodyMd,
            Setting = s.Setting,
            Era = s.Era,
            SettingDate = s.SettingDate,
            AuthorName = PublicNames.Of(null, scenario.Author),
            SourceScenarioId = s.SourceScenarioId,
            SourceScenarioName = scenario.SourceName,
            RunCount = scenario.Runs,
            UpdatedAt = s.UpdatedAt,
            CanEdit = rights.CanEdit,
            CanDelete = rights.CanDelete,
            Locations = [.. OrderTree(locations).Select(l => ScenarioParts.Location(l, tracks, skills))],
            KeyFacts = [.. facts.Select(ScenarioParts.Fact)],
            Handouts = [.. handouts.Select(ScenarioParts.Handout)],
            Creatures = [.. creatures],
            Items = [.. items],
            Npcs =
            [
                .. npcSummaries.Select(summary =>
                {
                    var row = cast.First(n => n.CharacterId == summary.Id);
                    return new ScenarioNpcDto(summary, row.Role, row.Count, row.Notes);
                }),
            ],
            Pregens = [.. pregenSummaries.Select(p => new ScenarioPregenDto(p, reserved.Contains(p.Id)))],
        };
    }

    /// <summary>Шапка: название, описание, место, эпоха, время действия — с версией корня.</summary>
    public async Task<ScenarioSavedDto> UpdateAsync(Guid scenarioId, ScenarioInput input, uint? ifMatch, CancellationToken cancellationToken)
    {
        await access.ForScenarioAsync(scenarioId, cancellationToken).Demand(Operation.Edit);
        return await WriteRootAsync(scenarioId, ifMatch, s => ApplyHeader(s, input), cancellationToken);
    }

    /// <summary>Основной текст Хранителя — отдельной записью: правка текста не трогает шапку, и наоборот.</summary>
    public async Task<ScenarioSavedDto> SaveTextAsync(Guid scenarioId, ScenarioTextInput input, uint? ifMatch, CancellationToken cancellationToken)
    {
        await access.ForScenarioAsync(scenarioId, cancellationToken).Demand(Operation.Edit);
        var body = Text(input.BodyMd, ScenarioLimits.BodyLength, "Текст сценария");
        return await WriteRootAsync(scenarioId, ifMatch, s => s.BodyMd = body, cancellationToken);
    }

    /// <summary>
    /// Удалить сценарий: автор или администратор. Прохождения держат его (<c>on delete restrict</c>) — 409 с текстом;
    /// части уходят каскадом, прегены сценария остаются в библиотеке (<c>set null</c>).
    /// </summary>
    public async Task DeleteAsync(Guid scenarioId, CancellationToken cancellationToken)
    {
        await access.ForScenarioAsync(scenarioId, cancellationToken).Demand(Operation.Delete);
        if (await dbContext.ScenarioRuns.AnyAsync(r => r.ScenarioId == scenarioId, cancellationToken))
        {
            throw ApiProblemException.InUse("Этот сценарий проходят в кампаниях — сначала уберите прохождения.");
        }

        await dbContext.Scenarios.Where(s => s.Id == scenarioId).ExecuteDeleteAsync(cancellationToken);
        logger.LogInformation("Удалён сценарий {ScenarioId}", scenarioId);
    }

    /// <summary>
    /// Порядок строк одной части: строки получают места по очереди. Локации — соседи одного родителя, проверки — одной
    /// локации; чужая строка — 400.
    /// </summary>
    public async Task ReorderAsync(Guid scenarioId, ReorderRequest request, CancellationToken cancellationToken)
    {
        await access.ForScenarioAsync(scenarioId, cancellationToken).Demand(Operation.Edit);
        var ids = request.Ids.Distinct().ToList();
        if (ids.Count != request.Ids.Count || ids.Count == 0)
        {
            throw ApiProblemException.Invalid("Порядок — список строк без повторов.");
        }

        int updated;
        switch (request.Part)
        {
            case ScenarioPart.Locations:
                var parents = await dbContext.ScenarioLocations.Where(l => l.ScenarioId == scenarioId && ids.Contains(l.Id))
                    .Select(l => l.ParentId).Distinct().ToListAsync(cancellationToken);
                if (parents.Count > 1)
                {
                    throw ApiProblemException.Invalid("Переставлять можно только соседние локации одного уровня.");
                }

                updated = await Reorder(dbContext.ScenarioLocations.Where(l => l.ScenarioId == scenarioId), l => l.Id, (l, o) => l.Ord = o);
                break;
            case ScenarioPart.Checks:
                var owners = await dbContext.ScenarioChecks
                    .Where(c => ids.Contains(c.Id) && dbContext.ScenarioLocations.Any(l => l.Id == c.LocationId && l.ScenarioId == scenarioId))
                    .Select(c => c.LocationId).Distinct().ToListAsync(cancellationToken);
                if (owners.Count > 1)
                {
                    throw ApiProblemException.Invalid("Переставлять можно только проверки одной локации.");
                }

                updated = await Reorder(dbContext.ScenarioChecks.Where(c => dbContext.ScenarioLocations
                    .Any(l => l.Id == c.LocationId && l.ScenarioId == scenarioId)), c => c.Id, (c, o) => c.Ord = o);
                break;
            case ScenarioPart.KeyFacts:
                updated = await Reorder(dbContext.ScenarioKeyFacts.Where(f => f.ScenarioId == scenarioId), f => f.Id, (f, o) => f.Ord = o);
                break;
            case ScenarioPart.Handouts:
                updated = await Reorder(dbContext.ScenarioHandouts.Where(h => h.ScenarioId == scenarioId), h => h.Id, (h, o) => h.Ord = o);
                break;
            case ScenarioPart.Creatures:
                updated = await Reorder(dbContext.ScenarioCreatures.Where(c => c.ScenarioId == scenarioId), c => c.Id, (c, o) => c.Ord = o);
                break;
            case ScenarioPart.Items:
                updated = await Reorder(dbContext.ScenarioItems.Where(i => i.ScenarioId == scenarioId), i => i.Id, (i, o) => i.Ord = o);
                break;
            default:
                throw ApiProblemException.Invalid("Неизвестная часть сценария.");
        }

        if (updated != ids.Count)
        {
            throw ApiProblemException.Invalid("В порядке есть строки не из этого сценария — перечитайте его.");
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return;

        async Task<int> Reorder<T>(IQueryable<T> scope, Func<T, Guid> id, Action<T, int> setOrd)
            where T : class
        {
            var rows = await scope.ToListAsync(cancellationToken);
            var found = 0;
            foreach (var row in rows)
            {
                var index = ids.IndexOf(id(row));
                if (index >= 0)
                {
                    setOrd(row, index);
                    found++;
                }
            }

            return found;
        }
    }

    /// <summary>Брони прегенов сценария в незавершённых прохождениях: таких не убрать.</summary>
    internal static async Task<HashSet<Guid>> ReservedPregensAsync(CmDbContext dbContext, Guid scenarioId, CancellationToken cancellationToken) =>
    [
        .. await dbContext.RunReservations
            .Where(r => dbContext.ScenarioRuns.Any(run => run.Id == r.RunId && run.ScenarioId == scenarioId
                                                                              && run.Status != ScenarioRunStatus.Finished))
            .Select(r => r.PregenId)
            .Distinct()
            .ToListAsync(cancellationToken),
    ];

    internal static string? Text(string? value, int limit, string field)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var text = value.Trim();
        return text.Length > limit ? throw ApiProblemException.Invalid($"{field} — не длиннее {limit} знаков.") : text;
    }

    internal static string Required(string? value, int limit, string field) =>
        Text(value, limit, field) ?? throw ApiProblemException.Invalid($"{field} — обязательно.");

    internal static void ApplyHeader(Scenario scenario, ScenarioInput input)
    {
        if (input.Era is { } era && !Enum.IsDefined(era))
        {
            throw ApiProblemException.Invalid("Неизвестная эпоха.");
        }

        scenario.Name = Required(input.Name, ScenarioLimits.NameLength, "Название сценария");
        scenario.Summary = Text(input.Summary, ScenarioLimits.TextLength, "Описание");
        scenario.Setting = Text(input.Setting, ScenarioLimits.ShortTextLength, "Место действия");
        scenario.Era = input.Era;
        scenario.SettingDate = Text(input.SettingDate, ScenarioLimits.ShortTextLength, "Время действия");
    }

    private async Task<ScenarioSavedDto> WriteRootAsync(Guid scenarioId, uint? ifMatch, Action<Scenario> change, CancellationToken cancellationToken)
    {
        if (ifMatch is not { } version)
        {
            throw ApiProblemException.VersionRequired();
        }

        var scenario = await dbContext.Scenarios.SingleAsync(s => s.Id == scenarioId, cancellationToken);
        if (scenario.Version != version)
        {
            throw ApiProblemException.Stale();
        }

        // Версия, с которой сравнивает сама запись (xmin): между чтением и UPDATE корень мог записать другой запрос.
        dbContext.Entry(scenario).Property(s => s.Version).OriginalValue = version;
        change(scenario);
        await dbContext.SaveChangesAsync(cancellationToken);
        return new ScenarioSavedDto(scenario.Version, scenario.UpdatedAt);
    }

    /// <summary>Локации деревом в глубину: родитель, затем его дети по порядку. Висячий родитель — на верхний уровень.</summary>
    internal static IEnumerable<ScenarioLocation> OrderTree(IReadOnlyList<ScenarioLocation> locations)
    {
        var ids = locations.Select(l => l.Id).ToHashSet();
        var children = locations.ToLookup(l => l.ParentId is { } parent && ids.Contains(parent) ? parent : (Guid?)null);
        return Walk(null);

        IEnumerable<ScenarioLocation> Walk(Guid? parent)
        {
            foreach (var location in children[parent].OrderBy(l => l.Ord).ThenBy(l => l.Id))
            {
                yield return location;
                foreach (var child in Walk(location.Id))
                {
                    yield return child;
                }
            }
        }
    }
}

/// <summary>Отображение строк частей в DTO — общее для рабочего места и ответов на запись.</summary>
internal static class ScenarioParts
{
    public static ScenarioLocationDto Location(ScenarioLocation l, IReadOnlyCollection<LocationTrack> tracks, IReadOnlyDictionary<Guid, string> skills) => new()
    {
        Id = l.Id,
        ParentId = l.ParentId,
        Ord = l.Ord,
        Name = l.Name,
        Address = l.Address,
        Description = l.Description,
        MusicTags = [.. l.MusicTags],
        TrackIds = [.. tracks.Where(t => t.LocationId == l.Id).Select(t => t.TrackId)],
        Checks = [.. l.Checks.OrderBy(c => c.Ord).Select(c => Check(c, skills))],
    };

    public static ScenarioCheckDto Check(ScenarioCheck c, IReadOnlyDictionary<Guid, string> skills) => new()
    {
        Id = c.Id,
        LocationId = c.LocationId,
        Ord = c.Ord,
        TargetKind = c.TargetKind,
        SkillId = c.SkillId,
        SkillName = c.SkillId is { } skill ? skills.GetValueOrDefault(skill) : null,
        Characteristic = c.Characteristic,
        Difficulty = c.Difficulty,
        OnSuccess = c.OnSuccess,
        OnFailure = c.OnFailure,
    };

    public static KeyFactDto Fact(ScenarioKeyFact f) => new(f.Id, f.Ord, f.Type, f.Title, f.Content);

    public static HandoutDto Handout(ScenarioHandout h) =>
        new(h.Id, h.Ord, h.Name, h.PlayerText, h.KeeperNote, h.FileId, h.FileId is { } file ? FilesRoutes.Content(file) : null);

    /// <summary>Твари с итоговым статблоком: своя версия сценария поверх бестиария.</summary>
    public static async Task<IReadOnlyList<ScenarioCreatureDto>> CreaturesAsync(CmDbContext db, IQueryable<ScenarioCreature> query,
        CancellationToken cancellationToken)
    {
        var rows = await query.AsNoTracking()
            .OrderBy(c => c.Ord)
            .Select(c => new
            {
                Row = c,
                Catalog = db.Creatures.Where(x => x.Id == c.CreatureId)
                    .Select(x => new
                    {
                        x.Name,
                        x.Type,
                        x.Statblock,
                        x.StatblockVersion,
                        Image = x.Images.OrderBy(i => i.Ord).Select(i => (Guid?)i.FileId).FirstOrDefault(),
                    })
                    .FirstOrDefault(),
            })
            .ToListAsync(cancellationToken);

        return
        [
            .. rows.Select(r =>
            {
                var c = r.Row;
                var own = c.Statblock is not null && c.StatblockVersion is not null;
                var statblock = own
                    ? CmJson.ReadStatblock(c.Statblock!, c.StatblockVersion!.Value)
                    : r.Catalog is { } catalog ? CmJson.ReadStatblock(catalog.Statblock, catalog.StatblockVersion) : new();
                return new ScenarioCreatureDto
                {
                    Id = c.Id,
                    Ord = c.Ord,
                    CreatureId = c.CreatureId,
                    CatalogName = r.Catalog?.Name,
                    OwnName = c.Name,
                    Name = c.Name ?? r.Catalog?.Name ?? "",
                    Type = r.Catalog?.Type,
                    Count = c.Count,
                    LocationNote = c.LocationNote,
                    Notes = c.Notes,
                    HasOwnStatblock = own,
                    Statblock = statblock,
                    ImageUrl = r.Catalog?.Image is { } image ? FilesRoutes.Content(image) : null,
                };
            }),
        ];
    }

    public static async Task<IReadOnlyList<ScenarioItemDto>> ItemsAsync(CmDbContext db, IQueryable<ScenarioItem> query,
        CancellationToken cancellationToken)
    {
        var rows = await query.AsNoTracking()
            .OrderBy(i => i.Ord)
            .Select(i => new
            {
                Row = i,
                Catalog = db.Items.Where(x => x.Id == i.ItemId).Select(x => new { x.Name, x.Type, x.Description, x.ImageFileId }).FirstOrDefault(),
            })
            .ToListAsync(cancellationToken);

        return
        [
            .. rows.Select(r => new ScenarioItemDto
            {
                Id = r.Row.Id,
                Ord = r.Row.Ord,
                ItemId = r.Row.ItemId,
                CatalogName = r.Catalog?.Name,
                OwnName = r.Row.Name,
                Name = r.Row.Name ?? r.Catalog?.Name ?? "",
                Type = r.Catalog?.Type,
                OwnDescription = r.Row.Description,
                Description = r.Row.Description ?? r.Catalog?.Description,
                LocationNote = r.Row.LocationNote,
                Notes = r.Row.Notes,
                ImageUrl = r.Catalog?.ImageFileId is { } image ? FilesRoutes.Content(image) : null,
            }),
        ];
    }
}
