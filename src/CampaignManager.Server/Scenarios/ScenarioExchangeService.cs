using CampaignManager.Contracts.Characters;
using CampaignManager.Contracts.Files;
using CampaignManager.Contracts.Scenarios;
using CampaignManager.Core;
using CampaignManager.Core.Campaigns;
using CampaignManager.Core.Catalogs;
using CampaignManager.Core.Characters;
using CampaignManager.Core.Documents;
using CampaignManager.Core.Music;
using CampaignManager.Core.Scenarios;
using CampaignManager.Data;
using CampaignManager.Data.Characters;
using CampaignManager.Data.Scenarios;
using CampaignManager.Server.Access;
using CampaignManager.Server.Characters;
using CampaignManager.Server.Platform;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace CampaignManager.Server.Scenarios;

/// <summary>
/// Сценарий целиком одним файлом (T2.5d): экспорт отдаёт то, что принимает импорт. Импорт <b>всегда</b> заводит новый
/// сценарий и идёт <b>одной транзакцией</b>: строки проверяются теми же правилами, что правка по строке
/// (<see cref="ScenarioPartsService"/>: <c>Apply*</c>), ошибка строки не останавливает разбор — отчёт перечисляет все, — но
/// хоть одна ошибка откатывает всё. Пробный прогон проходит ту же запись и откатывает её. Знание — <c>Scenarios/CLAUDE.md</c>.
/// </summary>
public sealed class ScenarioExchangeService(
    CmDbContext dbContext,
    AccessPolicy access,
    CurrentUser currentUser,
    ILogger<ScenarioExchangeService> logger)
{
    private const string Luck = "Удача";

    // ── Экспорт ─────────────────────────────────────────────────────

    /// <summary>Файл сценария: без идентификаторов, ссылки — по именам справочников, НПС — листами, как напечатаны.</summary>
    public async Task<ScenarioFile> ExportAsync(Guid scenarioId, CancellationToken cancellationToken)
    {
        await access.ForScenarioAsync(scenarioId, cancellationToken).Demand(Operation.Read);
        var user = await currentUser.GetAsync(cancellationToken) ?? throw AccessDeniedException.Forbidden();
        var scenario = await dbContext.Scenarios.AsNoTracking().SingleAsync(s => s.Id == scenarioId, cancellationToken);
        var catalog = await SkillCatalogAsync(cancellationToken);

        var locations = await dbContext.ScenarioLocations.AsNoTracking()
            .Where(l => l.ScenarioId == scenarioId).Include(l => l.Checks).ToListAsync(cancellationToken);
        var locationIds = locations.Select(l => l.Id).ToList();
        var tracks = await dbContext.LocationTracks.AsNoTracking()
            .Where(t => locationIds.Contains(t.LocationId))
            .Join(dbContext.MusicTracks, t => t.TrackId, m => m.Id, (t, m) => new { t.LocationId, m.Name })
            .ToListAsync(cancellationToken);
        var names = locations.ToDictionary(l => l.Id, l => l.Name);

        var facts = await dbContext.ScenarioKeyFacts.AsNoTracking().Where(f => f.ScenarioId == scenarioId).OrderBy(f => f.Ord)
            .ToListAsync(cancellationToken);
        var handouts = await dbContext.ScenarioHandouts.AsNoTracking().Where(h => h.ScenarioId == scenarioId).OrderBy(h => h.Ord)
            .ToListAsync(cancellationToken);
        var creatures = await dbContext.ScenarioCreatures.AsNoTracking().Where(c => c.ScenarioId == scenarioId).OrderBy(c => c.Ord)
            .Select(c => new { Row = c, Catalog = dbContext.Creatures.Where(x => x.Id == c.CreatureId).Select(x => x.Name).FirstOrDefault() })
            .ToListAsync(cancellationToken);
        var items = await dbContext.ScenarioItems.AsNoTracking().Where(i => i.ScenarioId == scenarioId).OrderBy(i => i.Ord)
            .Select(i => new { Row = i, Catalog = dbContext.Items.Where(x => x.Id == i.ItemId).Select(x => x.Name).FirstOrDefault() })
            .ToListAsync(cancellationToken);

        var cast = await dbContext.ScenarioNpcs.AsNoTracking().Where(n => n.ScenarioId == scenarioId)
            .Join(dbContext.Characters, n => n.CharacterId, c => c.Id, (n, c) => new { Cast = n, Character = c })
            .ToListAsync(cancellationToken);
        var pregens = await dbContext.Characters.AsNoTracking()
            .Where(c => c.ScenarioId == scenarioId && c.Kind == CharacterKind.Pregen && c.Status != CharacterStatus.Archived)
            .ToListAsync(cancellationToken);

        var file = new ScenarioFile
        {
            Name = scenario.Name,
            Description = scenario.Summary,
            Location = scenario.Setting,
            Era = scenario.SettingDate,
            Epoch = scenario.Era,
            Journal = scenario.BodyMd,
            KeyFacts = [.. facts.Select(f => new ScenarioFileFact { Title = f.Title, Type = f.Type, Content = f.Content })],
            Locations =
            [
                .. ScenarioService.OrderTree(locations).Select(l => new ScenarioFileLocation
                {
                    Name = l.Name,
                    Address = l.Address,
                    Description = l.Description,
                    Parent = l.ParentId is { } parent ? names.GetValueOrDefault(parent) : null,
                    SkillChecks = [.. l.Checks.OrderBy(c => c.Ord).Select(c => ExportCheck(c, catalog))],
                    MusicTags = NullIfEmpty(MusicTags.Normalize(l.MusicTags)),
                    Tracks = NullIfEmpty([.. tracks.Where(t => t.LocationId == l.Id).Select(t => t.Name).Order(StringComparer.Ordinal)]),
                }),
            ],
            Handouts =
            [
                .. handouts.Select(h => new ScenarioFileHandout
                {
                    Name = h.Name,
                    Description = h.PlayerText,
                    FileUrl = h.FileId is { } fileId ? FilesRoutes.Content(fileId) : null,
                    KeeperNote = h.KeeperNote,
                }),
            ],
            Creatures =
            [
                .. creatures.Select(c => new ScenarioFileCreature
                {
                    Creature = c.Catalog,
                    Name = c.Row.Name,
                    Count = c.Row.Count,
                    Location = c.Row.LocationNote,
                    Notes = c.Row.Notes,
                    Statblock = c.Row is { Statblock: { } own, StatblockVersion: { } version } ? WithoutSkillIds(CmJson.ReadStatblock(own, version)) : null,
                }),
            ],
            Items =
            [
                .. items.Select(i => new ScenarioFileItem
                {
                    Item = i.Catalog,
                    Name = i.Row.Name,
                    Description = i.Row.Description,
                    Location = i.Row.LocationNote,
                    Notes = i.Row.Notes,
                }),
            ],
            Npcs =
            [
                .. cast.OrderBy(c => c.Character.Name, StringComparer.CurrentCulture).ThenBy(c => c.Character.Id).Select(c =>
                {
                    var npc = ExportCharacter(new ScenarioFileNpc(), c.Character, catalog);
                    npc.Role = c.Cast.Role;
                    npc.Count = c.Cast.Count;
                    npc.Notes = c.Cast.Notes;
                    return npc;
                }),
            ],
            Pregens =
            [
                .. pregens.OrderBy(p => p.Name, StringComparer.CurrentCulture).ThenBy(p => p.Id)
                    .Select(p => ExportCharacter(new ScenarioFileCharacter(), p, catalog)),
            ],
        };

        logger.LogInformation("Экспорт сценария {ScenarioId} ({UserId})", scenarioId, user.Id);
        return file;
    }

    private static ScenarioFileCheck ExportCheck(ScenarioCheck check, SkillCatalog catalog) => new()
    {
        SkillName = check.TargetKind switch
        {
            CheckTarget.Characteristic when check.Characteristic is { } c =>
                InvestigatorCreationRules.Characteristics.First(i => i.Key == c).Abbreviation,
            CheckTarget.Luck => Luck,
            _ => catalog.Find(check.SkillId)?.Name ?? "",
        },
        Difficulty = check.Difficulty is Difficulty.Regular ? null : check.Difficulty.ToString(),
        SuccessResult = check.OnSuccess,
        FailureResult = check.OnFailure,
    };

    private static T ExportCharacter<T>(T target, Character character, SkillCatalog catalog)
        where T : ScenarioFileCharacter
    {
        var data = SheetBuilder.ToImport(CmJson.ReadSheet(character.Sheet, character.SheetVersion), catalog);
        target.Name = data.Name;
        target.Occupation = data.Occupation;
        target.Age = data.Age;
        target.Gender = data.Gender;
        target.Backstory = data.Backstory;
        target.Characteristics = data.Characteristics with { Extra = null };
        target.HitPoints = data.HitPoints;
        target.MagicPoints = data.MagicPoints;
        target.Sanity = data.Sanity;
        target.Luck = data.Luck;
        target.DamageBonus = data.DamageBonus;
        target.Build = data.Build;
        target.MoveSpeed = data.MoveSpeed;
        target.Dodge = data.Dodge;
        target.Skills = new Dictionary<string, int>(data.Skills);
        target.Birthplace = data.Birthplace;
        target.Residence = data.Residence;
        target.Biography = data.Biography;
        target.Weapons = NullIfEmpty(
        [
            .. data.Weapons.Select(w => new ScenarioFileWeapon
            {
                Name = w.Name,
                Skill = w.Skill,
                Damage = Blank(w.Damage),
                Range = Blank(w.Range),
                Attacks = Blank(w.Attacks),
                Ammo = Blank(w.Ammo),
                Malfunction = Blank(w.Malfunction),
                Impaling = w.Impaling,
                Notes = Blank(w.Notes),
            }),
        ]);
        target.Spells = NullIfEmpty(
        [
            .. data.Spells.Select(s => new ScenarioFileSpell
            {
                Name = s.Name,
                AlternativeNames = NullIfEmpty([.. s.AlternativeNames]),
                Cost = Blank(s.Cost),
                CastingTime = Blank(s.CastingTime),
                Description = Blank(s.Description),
            }),
        ]);
        target.Equipment = NullIfEmpty([.. data.Equipment.Select(e => e with { Extra = null })]);
        target.Finances = data.Finances is { } finances ? finances with { Extra = null } : null;
        target.PortraitUrl = character.PortraitFileId is { } portrait ? FilesRoutes.Content(portrait) : null;
        return target;
    }

    /// <summary>Навыки твари — по имени: id навыка в другой базе ничего не значит.</summary>
    private static Statblock WithoutSkillIds(Statblock statblock) =>
        statblock with { Skills = [.. statblock.Skills.Select(s => s with { SkillId = null })] };

    // ── Импорт ──────────────────────────────────────────────────────

    /// <summary>
    /// Новый сценарий из файла, автор — вошедший. Одна транзакция: любая строка с ошибкой — не записано ничего, отчёт
    /// перечисляет все строки. <paramref name="dryRun"/> — та же запись с откатом.
    /// </summary>
    /// <param name="name">Название копии вместо названия из файла.</param>
    public async Task<ScenarioImportReport> ImportAsync(ScenarioFile file, bool dryRun, string? name, CancellationToken cancellationToken)
    {
        await access.CanCreateScenarioAsync(cancellationToken).Demand();
        var user = await currentUser.GetAsync(cancellationToken) ?? throw AccessDeniedException.Forbidden();

        var scenario = new Scenario { Name = "", AuthorId = user.Id };
        var report = NewReport(file, scenario, name, dryRun);
        var sameName = scenario.Name.ToLower();
        if (await dbContext.Scenarios.AsNoTracking().AnyAsync(x => x.Name.ToLower() == sameName, cancellationToken))
        {
            report.Warnings.Add($"Сценарий «{scenario.Name}» в библиотеке уже есть — запишется второй с тем же названием. Чтобы их различать, допишите «(копия)» в поле «Название копии».");
        }

        var lookup = await ImportLookup.LoadAsync(dbContext, file, cancellationToken);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        dbContext.Scenarios.Add(scenario);
        return await WritePartsAsync(file, scenario, user, lookup, report, transaction, new HashSet<Guid>(), cancellationToken);
    }

    /// <summary>
    /// Заменить содержимое сценария файлом: id, автор и прохождения остаются, шапка, текст и все части — из файла (старые
    /// части удаляются, прегены уходят в архив, как при «Убрать»). Лист НПС, который заведён вошедшим и занят только в этом
    /// сценарии, обновляется из файла (<see cref="ScenarioImportOutcome.Updated"/>), а не занимается как есть. Та же одна
    /// транзакция и тот же пробный прогон, что у <see cref="ImportAsync"/>. Право — как у удаления (автор или админ);
    /// забронированные прегены — 409 до записи.
    /// </summary>
    public async Task<ScenarioImportReport> ReplaceAsync(Guid scenarioId, ScenarioFile file, bool dryRun, string? name,
        CancellationToken cancellationToken)
    {
        // Замена стирает всё содержимое разом — право как у удаления (автор или админ), а не как у правки по строке.
        await access.ForScenarioAsync(scenarioId, cancellationToken).Demand(Operation.Delete);
        var user = await currentUser.GetAsync(cancellationToken) ?? throw AccessDeniedException.Forbidden();
        if ((await ScenarioService.ReservedPregensAsync(dbContext, scenarioId, cancellationToken)).Count > 0)
        {
            throw ApiProblemException.Conflict("Готовых сыщиков этого сценария заняли игроки — сначала снимите записи, потом заменяйте.");
        }

        var scenario = await dbContext.Scenarios.SingleAsync(s => s.Id == scenarioId, cancellationToken);
        var report = NewReport(file, scenario, name, dryRun);
        report.Replaced = true;
        var sameName = scenario.Name.ToLower();
        if (await dbContext.Scenarios.AsNoTracking().AnyAsync(x => x.Id != scenarioId && x.Name.ToLower() == sameName, cancellationToken))
        {
            report.Warnings.Add($"В библиотеке есть другой сценарий «{scenario.Name}» — названия совпадут.");
        }

        var lookup = await ImportLookup.LoadAsync(dbContext, file, cancellationToken);
        var ownSheets = await OwnNpcSheetsAsync(scenarioId, user, cancellationToken);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        await ClearPartsAsync(scenarioId, cancellationToken);
        return await WritePartsAsync(file, scenario, user, lookup, report, transaction, ownSheets, cancellationToken);
    }

    /// <summary>Шапка и текст из файла — в строку сценария, и пустой отчёт; ошибка шапки — 400 целиком.</summary>
    private static ScenarioImportReport NewReport(ScenarioFile file, Scenario scenario, string? name, bool dryRun)
    {
        try
        {
            ScenarioService.ApplyHeader(scenario, new ScenarioInput(
                string.IsNullOrWhiteSpace(name) ? file.Name : name,
                file.Description,
                file.Location,
                file.Epoch ?? ScenarioEra.FromSettingDate(file.Era),
                file.Era));
            scenario.BodyMd = ScenarioService.Text(file.Journal, ScenarioLimits.BodyLength, "Текст сценария (journal)");
        }
        catch (ApiProblemException problem)
        {
            throw ApiProblemException.Invalid($"Шапка сценария: {problem.Message}");
        }

        var report = new ScenarioImportReport { DryRun = dryRun, ScenarioName = scenario.Name };
        if (file.IsTemplate is not null || file.IsPublished is not null || file.ScheduledDate is not null || file.AnnouncementText is not null)
        {
            report.Warnings.Add("Поля анонса v1 (isTemplate, isPublished, scheduledDate, announcementText) пропущены: анонс теперь у прохождения.");
        }

        return report;
    }

    /// <summary>Части файла — в сценарий, затем откат (пробный прогон или ошибки строк) или запись.</summary>
    private async Task<ScenarioImportReport> WritePartsAsync(ScenarioFile file, Scenario scenario, SignedInUser user, ImportLookup lookup,
        ScenarioImportReport report, IDbContextTransaction transaction, IReadOnlySet<Guid> ownSheets, CancellationToken cancellationToken)
    {
        ImportLocations(file, scenario, lookup, report);
        ImportFacts(file, scenario, report);
        ImportHandouts(file, scenario, lookup, report);
        ImportCreatures(file, scenario, lookup, report);
        ImportItems(file, scenario, lookup, report);
        if (report.Replaced)
        {
            MarkPartsAdded();
        }

        await SaveAsync(cancellationToken);

        await ImportCastAsync(file, scenario, user, lookup, report, ownSheets, cancellationToken);
        await ImportPregensAsync(file, scenario, user, lookup, report, cancellationToken);
        await SaveAsync(cancellationToken);

        report.Failed = report.Lines.Count(l => l.Outcome is ScenarioImportOutcome.Failed);
        if (report.DryRun || report.Failed > 0)
        {
            await transaction.RollbackAsync(cancellationToken);
            dbContext.ChangeTracker.Clear();
            return report;
        }

        await transaction.CommitAsync(cancellationToken);
        report.Imported = true;
        report.ScenarioId = scenario.Id;
        logger.LogInformation(
            "{Kind} сценария {ScenarioId}: {Locations} локаций, {Checks} проверок, {Npcs} новых НПС, {Updated} обновлённых, {Reused} занятых, {Pregens} прегенов",
            report.Replaced ? "Замена" : "Импорт", scenario.Id, report.Locations, report.Checks, report.NpcsCreated, report.NpcsUpdated,
            report.NpcsReused, report.Pregens);
        return report;
    }

    /// <summary>
    /// Замена: все части — новые строки (старые удалены <see cref="ClearPartsAsync"/>). Но сценарий уже отслеживается, и EF,
    /// найдя части по его навигациям с готовым Guid-ключом, считает часть из них существующими (Modified) — правим на Added.
    /// </summary>
    private void MarkPartsAdded()
    {
        dbContext.ChangeTracker.DetectChanges();
        foreach (var entry in dbContext.ChangeTracker.Entries()
                     .Where(e => e.State != EntityState.Added && e.Entity is ScenarioLocation or ScenarioCheck or ScenarioKeyFact
                         or ScenarioHandout or ScenarioCreature or ScenarioItem)
                     .ToList())
        {
            entry.State = EntityState.Added;
        }
    }

    /// <summary>
    /// Листы НПС, которые замена вправе переписать: в составе этого сценария, заведены вошедшим, без кампании и ни в каком
    /// другом сценарии не заняты. Чужие и общие листы замена занимает как есть, как и импорт.
    /// </summary>
    private async Task<HashSet<Guid>> OwnNpcSheetsAsync(Guid scenarioId, SignedInUser user, CancellationToken cancellationToken) =>
    [
        .. await dbContext.ScenarioNpcs.AsNoTracking().Where(n => n.ScenarioId == scenarioId)
            .Join(dbContext.Characters, n => n.CharacterId, c => c.Id, (n, c) => c)
            .Where(c => c.Kind == CharacterKind.Npc && c.CreatedById == user.Id && c.CampaignId == null
                        && !dbContext.ScenarioNpcs.Any(o => o.CharacterId == c.Id && o.ScenarioId != scenarioId))
            .Select(c => c.Id)
            .ToListAsync(cancellationToken),
    ];

    /// <summary>
    /// Замена: старые части — прочь (проверки и треки локаций уходят каскадом), состав снимается, прегены — в архив без
    /// сценария, как у «Убрать» (<see cref="ScenarioCastService.RemovePregenAsync"/>). Всё — в транзакции замены.
    /// </summary>
    private async Task ClearPartsAsync(Guid scenarioId, CancellationToken cancellationToken)
    {
        await dbContext.ScenarioLocations.Where(l => l.ScenarioId == scenarioId).ExecuteDeleteAsync(cancellationToken);
        await dbContext.ScenarioKeyFacts.Where(f => f.ScenarioId == scenarioId).ExecuteDeleteAsync(cancellationToken);
        await dbContext.ScenarioHandouts.Where(h => h.ScenarioId == scenarioId).ExecuteDeleteAsync(cancellationToken);
        await dbContext.ScenarioCreatures.Where(c => c.ScenarioId == scenarioId).ExecuteDeleteAsync(cancellationToken);
        await dbContext.ScenarioItems.Where(i => i.ScenarioId == scenarioId).ExecuteDeleteAsync(cancellationToken);
        await dbContext.ScenarioNpcs.Where(n => n.ScenarioId == scenarioId).ExecuteDeleteAsync(cancellationToken);

        var pregens = await dbContext.Characters
            .Where(c => c.ScenarioId == scenarioId && c.Kind == CharacterKind.Pregen)
            .ToListAsync(cancellationToken);
        foreach (var pregen in pregens)
        {
            pregen.ScenarioId = null;
            pregen.Status = CharacterStatus.Archived;
        }
    }

    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            // Правила строк ловят всё, что знают; сюда доходит только то, что держит сама база. Транзакция откатится.
            logger.LogWarning(ex, "Импорт сценария: база отказала в записи");
            throw ApiProblemException.Invalid($"База отказала в записи — ничего не записано: {ex.InnerException?.Message ?? ex.Message}");
        }
    }

    private static void ImportLocations(ScenarioFile file, Scenario scenario, ImportLookup lookup, ScenarioImportReport report)
    {
        var created = new ScenarioLocation?[file.Locations.Count];
        var notes = new List<string>[file.Locations.Count];
        var failed = new bool[file.Locations.Count];
        for (var i = 0; i < file.Locations.Count; i++)
        {
            var source = file.Locations[i];
            notes[i] = [];
            var location = new ScenarioLocation { ScenarioId = scenario.Id, Name = "" };
            try
            {
                ScenarioPartsService.ApplyLocationFields(location, new LocationInput(source.Name, source.Address, source.Description));
                location.MusicTags = ScenarioPartsService.LocationMusicTags(source.MusicTags);
            }
            catch (ApiProblemException problem)
            {
                failed[i] = true;
                report.Lines.Add(new ScenarioImportLine(ScenarioImportPart.Location, Label(source.Name, i), ScenarioImportOutcome.Failed, problem.Message));
                continue;
            }

            foreach (var track in source.Tracks ?? [])
            {
                if (Find(lookup.Tracks, track) is { } trackId)
                {
                    if (!lookup.NewTracks.Contains((location.Id, trackId)))
                    {
                        lookup.NewTracks.Add((location.Id, trackId));
                    }
                }
                else if (!string.IsNullOrWhiteSpace(track))
                {
                    notes[i].Add($"трека «{track.Trim()}» нет в фонотеке — не прибит");
                }
            }

            created[i] = location;
        }

        // Родитель — по имени: ближайшая выше по файлу локация с этим именем (экспорт пишет дерево в глубину, поэтому и
        // у одноимённых локаций родитель найдётся верный), иначе первая ниже.
        for (var i = 0; i < created.Length; i++)
        {
            if (created[i] is not { } location || string.IsNullOrWhiteSpace(file.Locations[i].Parent))
            {
                continue;
            }

            var parentName = file.Locations[i].Parent!.Trim();
            var key = Key(parentName);
            var candidates = Enumerable.Range(0, created.Length).Where(j => j != i && created[j] is not null && Key(created[j]!.Name) == key).ToList();
            var parentIndex = candidates.LastOrDefault(j => j < i, -1) is var before and >= 0 ? before : candidates.FirstOrDefault(j => j > i, -1);
            if (parentIndex < 0)
            {
                notes[i].Add(Enumerable.Range(0, created.Length).Any(j => failed[j] && Key(file.Locations[j].Name) == key)
                    ? $"родитель «{parentName}» не записан — локация на верхнем уровне"
                    : $"родителя «{parentName}» в файле нет — локация на верхнем уровне");
                continue;
            }

            location.ParentId = created[parentIndex]!.Id;
            if (HasCycle(location, created))
            {
                location.ParentId = null;
                notes[i].Add($"родитель «{parentName}» замкнул бы дерево в кольцо — локация на верхнем уровне");
            }
        }

        var ords = new Dictionary<Guid, int>();
        var topOrd = 0;
        for (var i = 0; i < created.Length; i++)
        {
            if (created[i] is not { } location)
            {
                continue;
            }

            location.Ord = location.ParentId is { } parent ? ords[parent] = ords.GetValueOrDefault(parent, -1) + 1 : topOrd++;
            scenario.Locations.Add(location);
            report.Locations++;
            report.Lines.Add(new ScenarioImportLine(ScenarioImportPart.Location, location.Name, ScenarioImportOutcome.Created, Join(notes[i])));
            ImportChecks(file.Locations[i], location, lookup, report);
        }

        foreach (var (locationId, trackId) in lookup.NewTracks)
        {
            lookup.Db.LocationTracks.Add(new LocationTrack { LocationId = locationId, TrackId = trackId });
        }
    }

    private static bool HasCycle(ScenarioLocation location, ScenarioLocation?[] all)
    {
        var byId = all.OfType<ScenarioLocation>().ToDictionary(l => l.Id);
        var seen = new HashSet<Guid>();
        for (Guid? current = location.ParentId; current is { } id; current = byId.GetValueOrDefault(id)?.ParentId)
        {
            if (id == location.Id || !seen.Add(id))
            {
                return true;
            }
        }

        return false;
    }

    private static void ImportChecks(ScenarioFileLocation source, ScenarioLocation location, ImportLookup lookup, ScenarioImportReport report)
    {
        foreach (var check in source.SkillChecks)
        {
            var name = $"{location.Name} › {check.SkillName?.Trim()}";
            try
            {
                var input = CheckInputOf(check, lookup.Resolver);
                var entity = new ScenarioCheck { LocationId = location.Id, Ord = location.Checks.Count };
                ScenarioPartsService.ApplyCheckFields(entity, input);
                location.Checks.Add(entity);
                report.Checks++;
                report.Lines.Add(new ScenarioImportLine(ScenarioImportPart.Check, name, ScenarioImportOutcome.Created, null));
            }
            catch (ApiProblemException problem)
            {
                report.Lines.Add(new ScenarioImportLine(ScenarioImportPart.Check, name, ScenarioImportOutcome.Failed, problem.Message));
            }
        }
    }

    /// <summary>Цель проверки по строке файла: «ИНТ» или «Интеллект» — характеристика, «Удача», иначе навык справочника.</summary>
    private static CheckInput CheckInputOf(ScenarioFileCheck check, SkillNameResolver resolver)
    {
        var difficulty = DifficultyOf(check.Difficulty);
        var target = check.SkillName?.Trim() ?? "";
        if (target.Length == 0)
        {
            throw ApiProblemException.Invalid("Не указан навык (skillName).");
        }

        var characteristic = InvestigatorCreationRules.Characteristics.FirstOrDefault(c =>
            Key(c.Abbreviation) == Key(target) || Key(c.Name) == Key(target));
        if (characteristic is not null)
        {
            return new CheckInput(CheckTarget.Characteristic, Characteristic: characteristic.Key, Difficulty: difficulty,
                OnSuccess: check.SuccessResult, OnFailure: check.FailureResult);
        }

        if (Key(target) == Key(Luck))
        {
            return new CheckInput(CheckTarget.Luck, Difficulty: difficulty, OnSuccess: check.SuccessResult, OnFailure: check.FailureResult);
        }

        return resolver.CatalogId(target) is { } skillId
            ? new CheckInput(CheckTarget.Skill, skillId, Difficulty: difficulty, OnSuccess: check.SuccessResult, OnFailure: check.FailureResult)
            : throw ApiProblemException.Invalid($"Навыка «{target}» нет в справочнике — исправьте имя (или «ИНТ», «Удача»).");
    }

    private static Difficulty DifficultyOf(string? text) => Key(text) switch
    {
        "" or "regular" or "обычная" => Difficulty.Regular,
        "hard" or "трудная" => Difficulty.Hard,
        "extreme" or "чрезвычайная" or "экстремальная" => Difficulty.Extreme,
        _ => throw ApiProblemException.Invalid($"Сложность «{text}» — пусто, Hard или Extreme."),
    };

    private static void ImportFacts(ScenarioFile file, Scenario scenario, ScenarioImportReport report)
    {
        for (var i = 0; i < file.KeyFacts.Count; i++)
        {
            var source = file.KeyFacts[i];
            var fact = new ScenarioKeyFact { ScenarioId = scenario.Id, Title = "", Ord = scenario.KeyFacts.Count };
            try
            {
                ScenarioPartsService.ApplyFact(fact, new KeyFactInput(source.Type, source.Title, source.Content));
            }
            catch (ApiProblemException problem)
            {
                report.Lines.Add(new ScenarioImportLine(ScenarioImportPart.KeyFact, Label(source.Title, i), ScenarioImportOutcome.Failed, problem.Message));
                continue;
            }

            scenario.KeyFacts.Add(fact);
            report.KeyFacts++;
            report.Lines.Add(new ScenarioImportLine(ScenarioImportPart.KeyFact, fact.Title, ScenarioImportOutcome.Created, null));
        }
    }

    private static void ImportHandouts(ScenarioFile file, Scenario scenario, ImportLookup lookup, ScenarioImportReport report)
    {
        for (var i = 0; i < file.Handouts.Count; i++)
        {
            var source = file.Handouts[i];
            var handout = new ScenarioHandout { ScenarioId = scenario.Id, Name = "", Ord = scenario.Handouts.Count };
            var fileId = lookup.FileOf(source.FileUrl);
            try
            {
                ScenarioPartsService.ApplyHandoutFields(handout, new HandoutInput(source.Name, source.Description, source.KeeperNote, fileId));
            }
            catch (ApiProblemException problem)
            {
                report.Lines.Add(new ScenarioImportLine(ScenarioImportPart.Handout, Label(source.Name, i), ScenarioImportOutcome.Failed, problem.Message));
                continue;
            }

            scenario.Handouts.Add(handout);
            report.Handouts++;
            report.Lines.Add(new ScenarioImportLine(ScenarioImportPart.Handout, handout.Name, ScenarioImportOutcome.Created,
                fileId is null && !string.IsNullOrWhiteSpace(source.FileUrl) ? "картинки нет в этой базе — загрузите её заново" : null));
        }
    }

    private static void ImportCreatures(ScenarioFile file, Scenario scenario, ImportLookup lookup, ScenarioImportReport report)
    {
        for (var i = 0; i < file.Creatures.Count; i++)
        {
            var source = file.Creatures[i];
            var label = Label(source.Name ?? source.Creature, i);
            var creature = new ScenarioCreature { ScenarioId = scenario.Id, Ord = scenario.Creatures.Count };
            try
            {
                if (Find(lookup.Creatures, source.Creature) is { } creatureId)
                {
                    creature.CreatureId = creatureId;
                }
                else if (source.Statblock is null || string.IsNullOrWhiteSpace(source.Name ?? source.Creature))
                {
                    throw ApiProblemException.Invalid(string.IsNullOrWhiteSpace(source.Creature)
                        ? "Укажите тварь бестиария (creature) или имя и статблок своей."
                        : $"Твари «{source.Creature.Trim()}» нет в бестиарии — заведите её там или приложите статблок.");
                }

                ScenarioPartsService.ApplyCreature(creature, new ScenarioCreatureInput(creature.CreatureId,
                    creature.CreatureId is null ? source.Name ?? source.Creature : source.Name, source.Count, source.Location, source.Notes));
                if (source.Statblock is { } statblock)
                {
                    creature.Statblock = CmJson.Write(WithSkillIds(statblock, lookup.Resolver));
                    creature.StatblockVersion = Statblock.CurrentVersion;
                }
            }
            catch (ApiProblemException problem)
            {
                report.Lines.Add(new ScenarioImportLine(ScenarioImportPart.Creature, label, ScenarioImportOutcome.Failed, problem.Message));
                continue;
            }

            scenario.Creatures.Add(creature);
            report.Creatures++;
            report.Lines.Add(new ScenarioImportLine(ScenarioImportPart.Creature, label, ScenarioImportOutcome.Created,
                creature.CreatureId is null ? "твари нет в бестиарии — заведена только для этого сценария" : null));
        }
    }

    /// <summary>Навыки своего статблока — по имени в этой базе; не нашлось — навык без ссылки (имя остаётся).</summary>
    private static Statblock WithSkillIds(Statblock statblock, SkillNameResolver resolver) =>
        statblock with { Skills = [.. statblock.Skills.Select(s => s with { SkillId = resolver.CatalogId(s.Name) })] };

    private static void ImportItems(ScenarioFile file, Scenario scenario, ImportLookup lookup, ScenarioImportReport report)
    {
        for (var i = 0; i < file.Items.Count; i++)
        {
            var source = file.Items[i];
            var label = Label(source.Name ?? source.Item, i);
            var itemId = Find(lookup.Items, source.Item);
            var item = new ScenarioItem { ScenarioId = scenario.Id, ItemId = itemId, Ord = scenario.Items.Count };
            var missing = itemId is null && !string.IsNullOrWhiteSpace(source.Item);
            try
            {
                // Предмета нет в справочнике — заводится своим реквизитом с тем же именем (а не отказом: реквизит сценария обычен).
                ScenarioPartsService.ApplyItem(item, new ScenarioItemInput(itemId, missing ? source.Name ?? source.Item : source.Name,
                    source.Description, source.Location, source.Notes));
            }
            catch (ApiProblemException problem)
            {
                report.Lines.Add(new ScenarioImportLine(ScenarioImportPart.Item, label, ScenarioImportOutcome.Failed, problem.Message));
                continue;
            }

            scenario.Items.Add(item);
            report.Items++;
            report.Lines.Add(new ScenarioImportLine(ScenarioImportPart.Item, label, ScenarioImportOutcome.Created,
                missing ? $"«{source.Item!.Trim()}» нет в справочнике — заведён своим реквизитом" : null));
        }
    }

    private async Task ImportCastAsync(ScenarioFile file, Scenario scenario, SignedInUser user, ImportLookup lookup,
        ScenarioImportReport report, IReadOnlySet<Guid> ownSheets, CancellationToken cancellationToken)
    {
        var library = await NpcLibraryAsync(user, cancellationToken);
        var cast = new HashSet<Guid>();
        for (var i = 0; i < file.Npcs.Count; i++)
        {
            var source = file.Npcs[i];
            var label = Label(source.Name, i);
            try
            {
                if (string.IsNullOrWhiteSpace(source.Name))
                {
                    throw ApiProblemException.Invalid("У НПС нет имени — по нему его ищут в библиотеке.");
                }

                if (!Enum.IsDefined(source.Role) || source.Count is < 1 or > CharacterLimits.MaxCastCount)
                {
                    throw ApiProblemException.Invalid($"Роль — Neutral, Enemy или Ally; количество — от 1 до {CharacterLimits.MaxCastCount}.");
                }

                var notes = ScenarioService.Text(source.Notes, ScenarioLimits.TextLength, "Заметка");
                string? message = null;
                Guid characterId;
                var outcome = ScenarioImportOutcome.Reused;
                if (Find(library, source.Name) is { } existing && ownSheets.Contains(existing))
                {
                    // Замена: свой лист этого сценария переписывается из файла — иначе правку статов в файле не донести.
                    characterId = existing;
                    message = await UpdateSheetAsync(existing, source, lookup, cancellationToken);
                    outcome = ScenarioImportOutcome.Updated;
                }
                else if (Find(library, source.Name) is { } shared)
                {
                    // Что параметры из файла к занятому листу не применяются, отчёт пишет один раз, а не в каждой строке.
                    characterId = shared;
                }
                else
                {
                    var (character, own) = await NewSheetAsync(source, CharacterKind.Npc, null, user, lookup, cancellationToken);
                    characterId = character.Id;
                    library[Key(source.Name)] = characterId;
                    message = own;
                    outcome = ScenarioImportOutcome.Created;
                }

                if (!cast.Add(characterId))
                {
                    throw ApiProblemException.Invalid("Этот НПС в файле уже есть — одно появление, сколько их — в count.");
                }

                dbContext.ScenarioNpcs.Add(new ScenarioNpc
                {
                    ScenarioId = scenario.Id, CharacterId = characterId, Role = source.Role, Count = source.Count, Notes = notes,
                });
                switch (outcome)
                {
                    case ScenarioImportOutcome.Created:
                        report.NpcsCreated++;
                        break;
                    case ScenarioImportOutcome.Updated:
                        report.NpcsUpdated++;
                        break;
                    default:
                        report.NpcsReused++;
                        break;
                }

                report.Lines.Add(new ScenarioImportLine(ScenarioImportPart.Npc, label, outcome, message));
            }
            catch (ApiProblemException problem)
            {
                report.Lines.Add(new ScenarioImportLine(ScenarioImportPart.Npc, label, ScenarioImportOutcome.Failed, problem.Message));
            }
        }
    }

    private async Task ImportPregensAsync(ScenarioFile file, Scenario scenario, SignedInUser user, ImportLookup lookup,
        ScenarioImportReport report, CancellationToken cancellationToken)
    {
        for (var i = 0; i < file.Pregens.Count; i++)
        {
            var source = file.Pregens[i];
            var label = Label(source.Name, i);
            try
            {
                if (string.IsNullOrWhiteSpace(source.Name))
                {
                    throw ApiProblemException.Invalid("У готового сыщика нет имени.");
                }

                var (_, own) = await NewSheetAsync(source, CharacterKind.Pregen, scenario.Id, user, lookup, cancellationToken);
                report.Pregens++;
                report.Lines.Add(new ScenarioImportLine(ScenarioImportPart.Pregen, label, ScenarioImportOutcome.Created, own));
            }
            catch (ApiProblemException problem)
            {
                report.Lines.Add(new ScenarioImportLine(ScenarioImportPart.Pregen, label, ScenarioImportOutcome.Failed, problem.Message));
            }
        }
    }

    /// <summary>
    /// Лист НПС или прегена — <see cref="SheetBuilder.FromImport"/>, как у помощника и быстрого НПС, и та же проверка документа,
    /// что у записи листа. Возвращает сообщение для отчёта: свои навыки, пропущенный портрет.
    /// </summary>
    private async Task<(Character Character, string? Message)> NewSheetAsync(ScenarioFileCharacter source, CharacterKind kind, Guid? scenarioId,
        SignedInUser user, ImportLookup lookup, CancellationToken cancellationToken)
    {
        var (sheet, own) = SheetBuilder.FromImport(ImportedOf(source, lookup), lookup.Catalog);
        await CharacterService.ValidateAsync(dbContext, sheet, cancellationToken);
        var portrait = lookup.FileOf(source.PortraitUrl);
        var character = new Character
        {
            Kind = kind,
            Status = CharacterStatus.Active,
            ScenarioId = scenarioId,
            PortraitFileId = portrait,
            Sheet = CmJson.Write(sheet),
            SheetVersion = CharacterSheet.CurrentVersion,
            CreatedById = user.Id,
        };
        dbContext.Characters.Add(character);
        return (character, SheetNotes(own, portrait, source, []));
    }

    /// <summary>Замена: свой лист НПС сценария — заново из файла (лист, портрет), с той же проверкой, что новый.</summary>
    private async Task<string?> UpdateSheetAsync(Guid characterId, ScenarioFileCharacter source, ImportLookup lookup,
        CancellationToken cancellationToken)
    {
        var (sheet, own) = SheetBuilder.FromImport(ImportedOf(source, lookup), lookup.Catalog);
        await CharacterService.ValidateAsync(dbContext, sheet, cancellationToken);
        var portrait = lookup.FileOf(source.PortraitUrl);
        var character = await dbContext.Characters.SingleAsync(c => c.Id == characterId, cancellationToken);
        character.Sheet = CmJson.Write(sheet);
        character.SheetVersion = CharacterSheet.CurrentVersion;
        character.PortraitFileId = portrait;
        return SheetNotes(own, portrait, source, ["лист обновлён из файла"]);
    }

    private static string? SheetNotes(IReadOnlyList<string> own, Guid? portrait, ScenarioFileCharacter source, List<string> notes)
    {
        if (own.Count > 0)
        {
            notes.Add($"нет в справочнике — свои навыки: {string.Join(", ", own)}");
        }

        if (portrait is null && !string.IsNullOrWhiteSpace(source.PortraitUrl))
        {
            notes.Add("портрета нет в этой базе — загрузите его заново");
        }

        return Join(notes);
    }

    private static ImportedCharacter ImportedOf(ScenarioFileCharacter source, ImportLookup lookup) => new()
    {
        Name = source.Name,
        Occupation = source.Occupation,
        Age = source.Age,
        Gender = source.Gender,
        Backstory = source.Backstory,
        Characteristics = source.Characteristics with { Extra = null },
        HitPoints = source.HitPoints,
        MagicPoints = source.MagicPoints,
        Sanity = source.Sanity,
        Luck = source.Luck,
        DamageBonus = source.DamageBonus,
        Build = source.Build,
        MoveSpeed = source.MoveSpeed,
        Dodge = source.Dodge,
        Skills = source.Skills,
        Birthplace = source.Birthplace,
        Residence = source.Residence,
        Biography = source.Biography,
        Weapons =
        [
            .. (source.Weapons ?? []).Where(w => !string.IsNullOrWhiteSpace(w.Name)).Select(w => new ImportedWeapon
            {
                Name = w.Name,
                Skill = w.Skill,
                Damage = w.Damage ?? "",
                Range = w.Range ?? "",
                Attacks = w.Attacks ?? "",
                Ammo = w.Ammo ?? "",
                Malfunction = w.Malfunction ?? "",
                Impaling = w.Impaling,
                Notes = w.Notes ?? "",
                CatalogWeaponId = Find(lookup.Weapons, w.Name),
            }),
        ],
        Spells =
        [
            .. (source.Spells ?? []).Where(s => !string.IsNullOrWhiteSpace(s.Name)).Select(s => new SheetSpell
            {
                CatalogSpellId = Find(lookup.Spells, s.Name),
                Name = s.Name.Trim(),
                AlternativeNames = [.. s.AlternativeNames ?? []],
                Cost = s.Cost ?? "",
                CastingTime = s.CastingTime ?? "",
                Description = s.Description ?? "",
            }),
        ],
        Equipment = [.. (source.Equipment ?? []).Where(e => !string.IsNullOrWhiteSpace(e.Name))],
        Finances = source.Finances,
    };

    /// <summary>
    /// НПС библиотеки, которых вошедший видит (правило листа), по имени без регистра и «ё»: при совпадении импорт занимает
    /// существующий лист, а не заводит двойника. Лист из библиотеки (без кампании) — впереди листа кампании.
    /// </summary>
    private async Task<Dictionary<string, Guid>> NpcLibraryAsync(SignedInUser user, CancellationToken cancellationToken)
    {
        var rows = await dbContext.Characters.AsNoTracking()
            .Where(c => c.Kind == CharacterKind.Npc && c.Status != CharacterStatus.Archived && c.Name != null)
            .Select(c => new
            {
                c.Id,
                c.Name,
                c.OwnerId,
                c.CampaignId,
                KeepsCampaign = c.CampaignId != null && dbContext.CampaignMembers.Any(m =>
                    m.CampaignId == c.CampaignId && m.UserId == user.Id && m.Role == CampaignRole.Keeper),
            })
            .ToListAsync(cancellationToken);

        var library = new Dictionary<string, Guid>(StringComparer.Ordinal);
        foreach (var row in rows
                     .Where(r => AccessPolicy.ForCharacter(user, CharacterKind.Npc, r.OwnerId, r.CampaignId, r.KeepsCampaign).CanRead)
                     .OrderBy(r => r.CampaignId is not null).ThenBy(r => r.Id))
        {
            library.TryAdd(Key(row.Name), row.Id);
        }

        return library;
    }

    private async Task<SkillCatalog> SkillCatalogAsync(CancellationToken cancellationToken) =>
        new(await dbContext.Skills.AsNoTracking()
            .Select(s => new SkillDefinition(s.Id, s.Name)
            {
                Code = s.Code, ParentId = s.ParentId, BaseValue = s.BaseValue, BaseFormula = s.BaseFormula, Category = s.Category,
            })
            .ToListAsync(cancellationToken));

    internal static string Key(string? name) => CatalogCodeTable.NormalizeName(name ?? "");

    /// <summary>Запись по имени без регистра и «ё»; нет — null (а не пустой Guid словаря).</summary>
    private static Guid? Find(Dictionary<string, Guid> byName, string? name) =>
        !string.IsNullOrWhiteSpace(name) && byName.TryGetValue(Key(name), out var id) ? id : null;

    private static string Label(string? name, int index) => string.IsNullOrWhiteSpace(name) ? $"(без имени, №{index + 1})" : name.Trim();

    private static string? Join(List<string> notes) => notes.Count == 0 ? null : string.Join("; ", notes);

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;

    private static List<T>? NullIfEmpty<T>(List<T> list) => list.Count == 0 ? null : list;

    /// <summary>Справочники, на которые файл ссылается по имени, — одним чтением до записи.</summary>
    private sealed class ImportLookup
    {
        public required CmDbContext Db { get; init; }
        public required SkillCatalog Catalog { get; init; }
        public required SkillNameResolver Resolver { get; init; }
        public required Dictionary<string, Guid> Tracks { get; init; }
        public required Dictionary<string, Guid> Creatures { get; init; }
        public required Dictionary<string, Guid> Items { get; init; }
        public required Dictionary<string, Guid> Weapons { get; init; }
        public required Dictionary<string, Guid> Spells { get; init; }
        public required HashSet<Guid> Files { get; init; }
        public List<(Guid Location, Guid Track)> NewTracks { get; } = [];

        /// <summary>Файл по адресу <c>/api/v1/files/{id}</c> (или полному): только если он есть в этой базе.</summary>
        public Guid? FileOf(string? url) => FileIdOf(url) is { } id && Files.Contains(id) ? id : null;

        public static async Task<ImportLookup> LoadAsync(CmDbContext db, ScenarioFile file, CancellationToken cancellationToken)
        {
            var catalog = new SkillCatalog(await db.Skills.AsNoTracking()
                .Select(s => new SkillDefinition(s.Id, s.Name)
                {
                    Code = s.Code, ParentId = s.ParentId, BaseValue = s.BaseValue, BaseFormula = s.BaseFormula, Category = s.Category,
                })
                .ToListAsync(cancellationToken));

            List<ScenarioFileCharacter> characters = [.. file.Npcs, .. file.Pregens];
            var fileIds = file.Handouts.Select(h => h.FileUrl).Concat(characters.Select(c => c.PortraitUrl))
                .Select(FileIdOf).OfType<Guid>().Distinct().ToList();

            return new ImportLookup
            {
                Db = db,
                Catalog = catalog,
                Resolver = new SkillNameResolver(catalog),
                Tracks = ByName(await db.MusicTracks.AsNoTracking().Select(t => new { t.Id, t.Name }).ToListAsync(cancellationToken), t => t.Name, t => t.Id),
                Creatures = ByName(await db.Creatures.AsNoTracking().Select(t => new { t.Id, t.Name }).ToListAsync(cancellationToken), t => t.Name, t => t.Id),
                Items = ByName(await db.Items.AsNoTracking().Select(t => new { t.Id, t.Name }).ToListAsync(cancellationToken), t => t.Name, t => t.Id),
                Weapons = characters.Any(c => c.Weapons is { Count: > 0 })
                    ? ByName(await db.Weapons.AsNoTracking().Select(t => new { t.Id, t.Name }).ToListAsync(cancellationToken), t => t.Name, t => t.Id)
                    : [],
                Spells = characters.Any(c => c.Spells is { Count: > 0 })
                    ? ByName(await db.Spells.AsNoTracking().Select(t => new { t.Id, t.Name }).ToListAsync(cancellationToken), t => t.Name, t => t.Id)
                    : [],
                Files = fileIds.Count == 0 ? [] : [.. await db.Files.AsNoTracking().Where(f => fileIds.Contains(f.Id)).Select(f => f.Id).ToListAsync(cancellationToken)],
            };
        }

        private static Dictionary<string, Guid> ByName<T>(IEnumerable<T> rows, Func<T, string> name, Func<T, Guid> id)
        {
            var result = new Dictionary<string, Guid>(StringComparer.Ordinal);
            foreach (var row in rows)
            {
                result.TryAdd(Key(name(row)), id(row));
            }

            return result;
        }

        private static Guid? FileIdOf(string? url)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                return null;
            }

            var path = Uri.TryCreate(url.Trim(), UriKind.Absolute, out var absolute) ? absolute.AbsolutePath : url.Trim();
            var prefix = FilesRoutes.Upload + "/";
            return path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                   && Guid.TryParse(path[prefix.Length..].TrimEnd('/'), out var id)
                ? id
                : (Guid?)null;
        }
    }
}
