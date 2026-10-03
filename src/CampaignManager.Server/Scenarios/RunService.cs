using CampaignManager.Contracts.Campaigns;
using CampaignManager.Contracts.Scenarios;
using CampaignManager.Core;
using CampaignManager.Core.Campaigns;
using CampaignManager.Core.Characters;
using CampaignManager.Data;
using CampaignManager.Data.Campaigns;
using CampaignManager.Data.Characters;
using CampaignManager.Server.Access;
using CampaignManager.Server.Campaigns;
using CampaignManager.Server.Platform;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CampaignManager.Server.Scenarios;

/// <summary>
/// Прохождения (T2.5c): сценарий библиотеки играют в кампании, содержимое не копируется. Ваншот — кампания
/// <see cref="CampaignKind.OneShot"/>, её Хранитель и прохождение с открытой записью, заведённые одной записью; бронь — копия
/// листа прегена игроку плюс строка <c>run_reservations</c>. В v1 публикация и бронь заводили кампанию каждая своим кодом,
/// бронь отдавала игроку сам преген (удаление кампании уносило его каскадом), а снять её мог любой Хранитель. Знание —
/// <c>Scenarios/CLAUDE.md</c>, «Прохождения и ваншоты».
/// </summary>
public sealed class RunService(CmDbContext dbContext, AccessPolicy access, CurrentUser currentUser, ILogger<RunService> logger)
{
    private const string OneActiveSheet =
        "У вас уже есть активный сыщик в этой кампании — сначала смените его статус на листе (выбыл, в отставке).";

    private const string AlreadyReserved = "Вы уже забронировали персонажа на эту игру — сначала снимите бронь.";
    private const string PregenTaken = "Этого персонажа уже забронировали — выберите другого.";

    /// <summary>
    /// Прохождения сценария в кампаниях, которые ведёт вошедший (администратору — все): только там ему отдадут сыщиков
    /// (<c>GET /campaigns/{id}/investigators</c> — Хранителю кампании), и только там он правит анонс и брони. Незавершённые —
    /// первыми, свежие — выше. Режим игры (T2.5b) и рабочее место читают этот же список.
    /// </summary>
    public async Task<IReadOnlyList<ScenarioRunDto>> ListAsync(Guid scenarioId, CancellationToken cancellationToken)
    {
        await access.ForScenarioAsync(scenarioId, cancellationToken).Demand(Operation.Read);
        var user = await RequireUserAsync(cancellationToken);

        var rows = await dbContext.ScenarioRuns.AsNoTracking()
            .Where(r => r.ScenarioId == scenarioId)
            .Select(r => new
            {
                r.Id,
                r.Status,
                r.ScheduledAt,
                r.CreatedAt,
                Role = dbContext.CampaignMembers.Where(m => m.CampaignId == r.CampaignId && m.UserId == user.Id)
                    .Select(m => (CampaignRole?)m.Role).FirstOrDefault(),
            })
            .ToListAsync(cancellationToken);

        var order = rows
            .Where(r => AccessPolicy.ForCampaign(user, r.Role).CanEdit)
            .OrderBy(r => r.Status == ScenarioRunStatus.Finished)
            .ThenByDescending(r => r.ScheduledAt ?? r.CreatedAt)
            .Select(r => r.Id)
            .ToList();
        var runs = await ReadAsync(order, user, cancellationToken);
        return [.. order.Select(id => runs.Single(r => r.Id == id))];
    }

    /// <summary>
    /// Незавершённые прохождения кампании со сценарием — для боя (T2.6d): окно участника предлагает сценарии этих
    /// прохождений. Права те же, что у <see cref="ListAsync"/>: Хранитель (сценарии видит Хранитель) и правка кампании.
    /// <paramref name="withRun"/> — прохождение сцены: оно в списке, даже завершённое (перенос завершил прохождения ваншотов, и
    /// окно участника не находило сценарий сцены).
    /// </summary>
    public async Task<IReadOnlyList<ScenarioRunDto>> ListForCampaignAsync(Guid campaignId, Guid? withRun, CancellationToken cancellationToken)
    {
        await access.CanBrowseScenariosAsync(cancellationToken).Demand();
        await access.ForCampaignAsync(campaignId, cancellationToken).Demand(Operation.Edit);
        var user = await RequireUserAsync(cancellationToken);

        var order = await dbContext.ScenarioRuns.AsNoTracking()
            .Where(r => r.CampaignId == campaignId && (r.Status != ScenarioRunStatus.Finished || r.Id == withRun))
            .Select(r => new { r.Id, r.ScheduledAt, r.CreatedAt })
            .ToListAsync(cancellationToken);
        var ids = order.OrderByDescending(r => r.ScheduledAt ?? r.CreatedAt).Select(r => r.Id).ToList();
        var runs = await ReadAsync(ids, user, cancellationToken);
        return [.. ids.Select(id => runs.Single(r => r.Id == id))];
    }

    /// <summary>
    /// «Играть в кампании»: прохождение без копии содержимого. Сценарий видит Хранитель, кампанию правит её Хранитель.
    /// Незавершённое прохождение того же сценария в той же кампании уже есть — 409: второе запутало бы журнал и режим игры.
    /// </summary>
    public async Task<ScenarioRunDto> PlayInCampaignAsync(Guid scenarioId, PlayInCampaignRequest request, CancellationToken cancellationToken)
    {
        await access.ForScenarioAsync(scenarioId, cancellationToken).Demand(Operation.Read);
        await access.ForCampaignAsync(request.CampaignId, cancellationToken).Demand(Operation.Edit);
        var user = await RequireUserAsync(cancellationToken);

        var campaign = await dbContext.Campaigns.AsNoTracking().Where(c => c.Id == request.CampaignId)
            .Select(c => new { c.Name, c.Status }).SingleAsync(cancellationToken);
        if (campaign.Status is CampaignStatus.Completed)
        {
            throw ApiProblemException.Conflict($"Кампания «{campaign.Name}» завершена — в ней уже не играют.");
        }

        if (await dbContext.ScenarioRuns.AnyAsync(r => r.ScenarioId == scenarioId && r.CampaignId == request.CampaignId
                                                       && r.Status != ScenarioRunStatus.Finished, cancellationToken))
        {
            throw ApiProblemException.Conflict($"Этот сценарий уже проходят в кампании «{campaign.Name}».");
        }

        var run = new ScenarioRun
        {
            ScenarioId = scenarioId,
            CampaignId = request.CampaignId,
            Status = ScenarioRunStatus.Planned,
            ScheduledAt = request.ScheduledAt?.ToUniversalTime(),
        };
        dbContext.ScenarioRuns.Add(run);
        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Сценарий {ScenarioId} играют в кампании {CampaignId}: прохождение {RunId}", scenarioId, request.CampaignId, run.Id);
        return (await ReadAsync([run.Id], user, cancellationToken)).Single();
    }

    /// <summary>
    /// «Объявить ваншот»: кампания вида <see cref="CampaignKind.OneShot"/>, вошедший — её Хранитель-участник, прохождение
    /// объявлено и запись открыта — **одной** записью (<c>SaveChanges</c> — одна транзакция): либо всё, либо ничего. В v1
    /// публикация и первая бронь заводили кампанию каждая своим кодом.
    /// </summary>
    public async Task<ScenarioRunDto> AnnounceOneShotAsync(Guid scenarioId, AnnounceOneShotRequest request, CancellationToken cancellationToken)
    {
        await access.ForScenarioAsync(scenarioId, cancellationToken).Demand(Operation.Read);
        await access.CanCreateCampaignAsync(cancellationToken).Demand();
        var user = await RequireUserAsync(cancellationToken);
        if (request.Era is { } requestedEra && !Enum.IsDefined(requestedEra))
        {
            throw ApiProblemException.Invalid("Неизвестная эпоха.");
        }

        var scenario = await dbContext.Scenarios.AsNoTracking().Where(s => s.Id == scenarioId)
            .Select(s => new { s.Name, s.Era }).SingleAsync(cancellationToken);
        var name = CampaignName(request.CampaignName, scenario.Name);
        var announcement = Announcement(request.Announcement);

        var campaign = new Campaign
        {
            Name = name,
            Kind = CampaignKind.OneShot,
            Status = CampaignStatus.Planning,
            Era = request.Era ?? scenario.Era ?? Era.Classic,
            CreatedById = user.Id,
        };
        campaign.Members.Add(new CampaignMember { UserId = user.Id, Role = CampaignRole.Keeper });
        var run = new ScenarioRun
        {
            ScenarioId = scenarioId,
            CampaignId = campaign.Id,
            Status = ScenarioRunStatus.Announced,
            ScheduledAt = request.ScheduledAt?.ToUniversalTime(),
            Announcement = announcement,
            SignupOpen = true,
        };
        dbContext.Campaigns.Add(campaign);
        dbContext.ScenarioRuns.Add(run);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Ваншот по сценарию {ScenarioId}: кампания {CampaignId}, прохождение {RunId}, Хранитель {UserId}",
            scenarioId, campaign.Id, run.Id, user.Id);
        return (await ReadAsync([run.Id], user, cancellationToken)).Single();
    }

    /// <summary>
    /// Одна форма прохождения: состояние, дата, анонс, запись (в v1 анонс правился в двух формах сценария, и сохранение
    /// одной затирало правки локаций). Завершённое прохождение запись закрывает само. Версии у строки нет: два устройства —
    /// «последний побеждает» в пределах прохождения.
    /// </summary>
    public async Task<ScenarioRunDto> UpdateAsync(Guid runId, RunInput input, CancellationToken cancellationToken)
    {
        await access.ForRunAsync(runId, cancellationToken).Demand(Operation.Edit);
        var user = await RequireUserAsync(cancellationToken);
        if (!Enum.IsDefined(input.Status))
        {
            throw ApiProblemException.Invalid("Неизвестное состояние прохождения.");
        }

        var run = await dbContext.ScenarioRuns.SingleAsync(r => r.Id == runId, cancellationToken);
        run.Status = input.Status;
        run.ScheduledAt = input.ScheduledAt?.ToUniversalTime();
        run.Announcement = Announcement(input.Announcement);
        run.SignupOpen = input.SignupOpen && input.Status is not ScenarioRunStatus.Finished;
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Прохождение {RunId}: {Status}, запись {SignupOpen}", runId, run.Status, run.SignupOpen);
        return (await ReadAsync([runId], user, cancellationToken)).Single();
    }

    /// <summary>
    /// Удалить прохождение: брони — каскадом, копии листов остаются у игроков в кампании, встречи журнала теряют ссылку
    /// (<c>SET NULL</c>). Кампания-ваншот остаётся — её удаляют отдельно, на странице кампании.
    /// </summary>
    public async Task DeleteAsync(Guid runId, CancellationToken cancellationToken)
    {
        await access.ForRunAsync(runId, cancellationToken).Demand(Operation.Delete);
        await dbContext.ScenarioRuns.Where(r => r.Id == runId).ExecuteDeleteAsync(cancellationToken);
        logger.LogInformation("Прохождение {RunId} удалено", runId);
    }

    /// <summary>
    /// Бронь прегена — любой вошедший, пока запись открыта (<see cref="AccessPolicy.CanReserveAsync"/>):
    /// <list type="bullet">
    /// <item>преген — лист этого сценария, не в архиве (чужой по адресу этого прохождения — 404);</item>
    /// <item>ведущий своей игры не бронирует; не участник кампании становится им (решение владельца, T2.2: в ваншот
    /// вступают и участником, и бронью);</item>
    /// <item>копия листа (документ и портрет) — сыщик игрока в кампании прохождения, <c>origin_character_id</c> — преген;
    /// сам преген остаётся в сценарии для следующего прохождения;</item>
    /// <item>повтор и чужая бронь отсекаются ключами <c>run_reservations</c> (<c>(run_id, pregen_id)</c> и
    /// <c>(run_id, user_id)</c>), второй активный сыщик — индексом <c>characters_one_active_sheet</c>: проверка до записи
    /// даёт понятный текст, ключ — гонку двух вкладок.</item>
    /// </list>
    /// Участник, копия и бронь — одной записью.
    /// </summary>
    public async Task<ReservationDto> ReserveAsync(Guid runId, Guid pregenId, CancellationToken cancellationToken)
    {
        var user = await RequireUserAsync(cancellationToken);
        var run = await dbContext.ScenarioRuns.AsNoTracking().Where(r => r.Id == runId)
                      .Select(r => new
                      {
                          r.CampaignId,
                          r.ScenarioId,
                          r.ScheduledAt,
                          CampaignStatus = dbContext.Campaigns.Where(c => c.Id == r.CampaignId).Select(c => c.Status).First(),
                          Role = dbContext.CampaignMembers.Where(m => m.CampaignId == r.CampaignId && m.UserId == user.Id)
                              .Select(m => (CampaignRole?)m.Role).FirstOrDefault(),
                      })
                      .SingleOrDefaultAsync(cancellationToken)
                  ?? throw AccessDeniedException.NotFound();
        if (run.ScheduledAt is { } at && at <= DateTimeOffset.UtcNow)
        {
            // Игра уже была: запись закрылась сама, Хранителю её открывать заново не нужно.
            throw ApiProblemException.Conflict("Игра уже прошла — запись закрыта.");
        }

        await access.CanReserveAsync(runId, cancellationToken).Demand();

        var pregen = await dbContext.Characters.AsNoTracking()
                         .SingleOrDefaultAsync(c => c.Id == pregenId && c.Kind == CharacterKind.Pregen && c.ScenarioId == run.ScenarioId
                                                    && c.Status != CharacterStatus.Archived, cancellationToken)
                     ?? throw AccessDeniedException.NotFound();

        if (run.Role is CampaignRole.Keeper)
        {
            throw ApiProblemException.Invalid("Вы ведёте эту игру — прегенов бронируют игроки.");
        }

        var reservations = await dbContext.RunReservations.AsNoTracking()
            .Where(r => r.RunId == runId && (r.UserId == user.Id || r.PregenId == pregenId))
            .Select(r => new { r.UserId, r.PregenId })
            .ToListAsync(cancellationToken);
        if (reservations.Any(r => r.UserId == user.Id))
        {
            throw ApiProblemException.Duplicate(AlreadyReserved);
        }

        if (reservations.Any(r => r.PregenId == pregenId))
        {
            throw ApiProblemException.Conflict(PregenTaken);
        }

        if (run.Role is null && run.CampaignStatus is CampaignStatus.Completed)
        {
            throw ApiProblemException.Conflict("Кампания завершена — в неё уже не вступить.");
        }

        if (run.Role is not null && await dbContext.Characters.AnyAsync(c => c.CampaignId == run.CampaignId && c.OwnerId == user.Id
                                                                            && c.Kind == CharacterKind.Player
                                                                            && c.Status == CharacterStatus.Active, cancellationToken))
        {
            throw ApiProblemException.Conflict(OneActiveSheet);
        }

        if (run.Role is null)
        {
            dbContext.CampaignMembers.Add(new CampaignMember { CampaignId = run.CampaignId, UserId = user.Id, Role = CampaignRole.Player });
        }

        var copy = new Character
        {
            Kind = CharacterKind.Player,
            Status = CharacterStatus.Active,
            OwnerId = user.Id,
            CampaignId = run.CampaignId,
            OriginCharacterId = pregen.Id,
            PortraitFileId = pregen.PortraitFileId,
            Sheet = pregen.Sheet,
            SheetVersion = pregen.SheetVersion,
            CreatedById = user.Id,
        };
        dbContext.Characters.Add(copy);
        dbContext.RunReservations.Add(new RunReservation { RunId = runId, PregenId = pregenId, UserId = user.Id, CharacterId = copy.Id });

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } unique)
        {
            // Проверки выше прошли у двух вкладок сразу — пустил ключ одну.
            throw unique.ConstraintName switch
            {
                "pk_run_reservations" => ApiProblemException.Conflict(PregenTaken),
                "ix_run_reservations_run_id_user_id" => ApiProblemException.Duplicate(AlreadyReserved),
                "pk_campaign_members" => ApiProblemException.Conflict("Вы вступили в кампанию с другой вкладки — повторите бронь."),
                _ => ApiProblemException.Conflict(OneActiveSheet),
            };
        }

        logger.LogInformation("Преген {PregenId} забронирован в прохождении {RunId} пользователем {UserId}: лист {CharacterId}",
            pregenId, runId, user.Id, copy.Id);
        return new ReservationDto(runId, pregenId, copy.Id, run.CampaignId);
    }

    /// <summary>
    /// Снять бронь — сам игрок или Хранитель кампании прохождения (<see cref="AccessPolicy.ForReservationAsync"/>; в v1 —
    /// любой Хранитель). Копия листа уходит в архив, а не удаляется: игрок мог её уже править. Участником кампании игрок
    /// остаётся (выйти — на странице кампании). Бронь сыгранной игры не снимается: лист там уже настоящий.
    /// </summary>
    public async Task ReleaseAsync(Guid runId, Guid pregenId, CancellationToken cancellationToken)
    {
        await access.ForReservationAsync(runId, pregenId, cancellationToken).Demand(Operation.Delete);
        if (await dbContext.ScenarioRuns.AnyAsync(r => r.Id == runId && r.Status == ScenarioRunStatus.Finished, cancellationToken))
        {
            throw ApiProblemException.Conflict("Игра уже сыграна — бронь осталась в истории прохождения.");
        }

        var reservation = await dbContext.RunReservations.SingleAsync(r => r.RunId == runId && r.PregenId == pregenId, cancellationToken);
        if (reservation.CharacterId is { } copyId
            && await dbContext.Characters.SingleOrDefaultAsync(c => c.Id == copyId && c.Status == CharacterStatus.Active, cancellationToken)
                is { } copy)
        {
            copy.Status = CharacterStatus.Archived;
        }

        dbContext.RunReservations.Remove(reservation);
        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Бронь прегена {PregenId} в прохождении {RunId} снята", pregenId, runId);
    }

    /// <summary>Прохождения по id с бронями — одна сборка DTO для списка и ответов записи.</summary>
    private async Task<List<ScenarioRunDto>> ReadAsync(IReadOnlyCollection<Guid> runIds, SignedInUser user, CancellationToken cancellationToken)
    {
        var runs = await dbContext.ScenarioRuns.AsNoTracking()
            .Where(r => runIds.Contains(r.Id))
            .Select(r => new
            {
                r.Id,
                r.CampaignId,
                Campaign = dbContext.Campaigns.Where(c => c.Id == r.CampaignId).Select(c => new { c.Name, c.Kind }).First(),
                r.ScenarioId,
                ScenarioName = dbContext.Scenarios.Where(s => s.Id == r.ScenarioId).Select(s => s.Name).First(),
                r.Status,
                r.ScheduledAt,
                r.Announcement,
                r.SignupOpen,
                r.CreatedAt,
                Role = dbContext.CampaignMembers.Where(m => m.CampaignId == r.CampaignId && m.UserId == user.Id)
                    .Select(m => (CampaignRole?)m.Role).FirstOrDefault(),
            })
            .ToListAsync(cancellationToken);

        var reservations = await dbContext.RunReservations.AsNoTracking()
            .Where(rr => runIds.Contains(rr.RunId))
            .OrderBy(rr => rr.CreatedAt)
            .Select(rr => new
            {
                rr.RunId,
                rr.PregenId,
                rr.UserId,
                rr.CharacterId,
                PregenName = dbContext.Characters.Where(c => c.Id == rr.PregenId).Select(c => c.Name).First(),
                Alias = dbContext.ScenarioRuns.Where(r => r.Id == rr.RunId)
                    .SelectMany(r => dbContext.CampaignMembers.Where(m => m.CampaignId == r.CampaignId && m.UserId == rr.UserId))
                    .Select(m => m.DisplayName)
                    .FirstOrDefault(),
                UserName = dbContext.Users.Where(u => u.Id == rr.UserId).Select(u => u.DisplayName).First(),
            })
            .ToListAsync(cancellationToken);

        return
        [
            .. runs.Select(r =>
            {
                var rights = AccessPolicy.ForCampaign(user, r.Role);
                return new ScenarioRunDto(
                    r.Id,
                    r.CampaignId,
                    r.Campaign.Name,
                    r.Campaign.Kind,
                    r.Status,
                    r.ScheduledAt,
                    r.Announcement,
                    AccessPolicy.SignupOpenNow(r.SignupOpen, r.ScheduledAt, r.Status),
                    [
                        .. reservations.Where(rr => rr.RunId == r.Id).Select(rr => new RunReservationDto(
                            rr.PregenId,
                            rr.PregenName ?? "Без имени",
                            PublicNames.Of(rr.Alias, rr.UserName),
                            rr.CharacterId,
                            // Правило AccessPolicy.ForReservationAsync для прочитанной строки.
                            rights.CanEdit || rr.UserId == user.Id)),
                    ],
                    rights.CanEdit,
                    rights.CanDelete,
                    r.ScenarioId,
                    r.ScenarioName);
            }),
        ];
    }

    private static string CampaignName(string? requested, string scenarioName)
    {
        var name = string.IsNullOrWhiteSpace(requested) ? $"{scenarioName} (ваншот)" : requested.Trim();
        if (name.Length > CampaignLimits.NameLength)
        {
            if (!string.IsNullOrWhiteSpace(requested))
            {
                throw ApiProblemException.Invalid($"Название кампании — не длиннее {CampaignLimits.NameLength} символов.");
            }

            name = name[..CampaignLimits.NameLength];
        }

        return name;
    }

    private static string? Announcement(string? value) =>
        ScenarioService.Text(value, RunLimits.AnnouncementLength, "Анонс");

    private async Task<SignedInUser> RequireUserAsync(CancellationToken cancellationToken) =>
        await currentUser.GetAsync(cancellationToken) ?? throw AccessDeniedException.Forbidden();
}
