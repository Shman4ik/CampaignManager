using System.Text;
using CampaignManager.Contracts.Encounters;
using CampaignManager.Core.Documents;
using CampaignManager.Core.Encounters;
using CampaignManager.Core.Encounters.Chase;
using CampaignManager.Data;
using CampaignManager.Data.Encounters;
using CampaignManager.Server.Access;
using CampaignManager.Server.Platform;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CampaignManager.Server.Encounters;

/// <summary>
/// Сцены: начать, прочитать, записать состояние с <c>If-Match</c>, завершить. Права — <see cref="AccessPolicy"/>: сцену видит
/// и правит только тот, кто её ведёт (<see cref="AccessPolicy.ForEncounterAsync"/>), начать — Хранитель, в кампании — её
/// Хранитель. Правила сцены сервер не исполняет: их исполняет Core на клиенте, сервер хранит документ и проверяет пределы.
/// <para>
/// В v1 бой не сохранялся вовсе (жил в circuit), а погоня писала снимок после каждого рендера вместе с полными
/// листами и картинками тварей. Здесь документ — снимок чисел, пишется автосохранением с версией: две вкладки одного
/// Хранителя не затирают друг друга молча.
/// </para>
/// </summary>
public sealed class EncounterService(CmDbContext dbContext, AccessPolicy access, CurrentUser currentUser, ILogger<EncounterService> logger)
{
    /// <summary>Активные сцены вошедшего — продолжить начатое. Сцены чужих Хранителей не видны и администратору.</summary>
    public async Task<IReadOnlyList<EncounterSummaryDto>> ListActiveAsync(EncounterKind? kind, CancellationToken cancellationToken)
    {
        if (await currentUser.GetAsync(cancellationToken) is not { IsKeeper: true } user)
        {
            return [];
        }

        var rows = await dbContext.Encounters.AsNoTracking()
            .Where(e => e.KeeperId == user.Id && e.Status == EncounterStatus.Active && (kind == null || e.Kind == kind))
            .OrderByDescending(e => e.UpdatedAt)
            .Select(e => new
            {
                e.Id,
                e.Kind,
                e.CampaignId,
                CampaignName = dbContext.Campaigns.Where(c => c.Id == e.CampaignId).Select(c => c.Name).FirstOrDefault(),
                e.State,
                e.StateVersion,
                e.UpdatedAt,
            })
            .ToListAsync(cancellationToken);

        return
        [
            .. rows.Select(r =>
            {
                var state = CmJson.ReadEncounterState(r.State, r.StateVersion);
                return new EncounterSummaryDto(r.Id, r.Kind, r.CampaignId, r.CampaignName, state.Round, state.Participants.Count, r.UpdatedAt);
            }),
        ];
    }

    /// <summary>
    /// Новая сцена. У Хранителя одна активная сцена вида на кампанию (и одна вне кампаний) — уникальный индекс
    /// <c>encounters_one_active</c>; вторая — 409 с текстом, клиент открывает уже идущую.
    /// </summary>
    public async Task<EncounterDto> StartAsync(StartEncounterRequest request, CancellationToken cancellationToken)
    {
        if (!Enum.IsDefined(request.Kind))
        {
            throw ApiProblemException.Invalid("Неизвестный вид сцены.");
        }

        if (request.CampaignId is { } campaignId && !await dbContext.Campaigns.AnyAsync(c => c.Id == campaignId, cancellationToken))
        {
            throw ApiProblemException.Invalid("Кампании нет — выберите другую.");
        }

        await access.CanStartEncounterAsync(request.CampaignId, cancellationToken).Demand();
        var user = await currentUser.GetAsync(cancellationToken) ?? throw AccessDeniedException.Forbidden();

        var encounter = new Encounter
        {
            KeeperId = user.Id,
            CampaignId = request.CampaignId,
            Kind = request.Kind,
            Status = EncounterStatus.Active,
            State = CmJson.Write(new EncounterState()),
            StateVersion = EncounterState.CurrentVersion,
        };
        dbContext.Encounters.Add(encounter);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            throw ApiProblemException.Conflict(
                $"{EncounterText.Of(request.Kind)} в этой кампании уже идёт — продолжите её или завершите.");
        }

        logger.LogInformation("Сцена {EncounterId} ({Kind}) начата, кампания {CampaignId}", encounter.Id, encounter.Kind, encounter.CampaignId);
        return await GetAsync(encounter.Id, cancellationToken);
    }

    public async Task<EncounterDto> GetAsync(Guid encounterId, CancellationToken cancellationToken)
    {
        await access.ForEncounterAsync(encounterId, cancellationToken).Demand(Operation.Read);
        var row = await dbContext.Encounters.AsNoTracking()
            .Where(e => e.Id == encounterId)
            .Select(e => new
            {
                Encounter = e,
                CampaignName = dbContext.Campaigns.Where(c => c.Id == e.CampaignId).Select(c => c.Name).FirstOrDefault(),
            })
            .SingleAsync(cancellationToken);

        var encounter = row.Encounter;
        return new EncounterDto
        {
            Id = encounter.Id,
            Kind = encounter.Kind,
            Status = encounter.Status,
            CampaignId = encounter.CampaignId,
            CampaignName = row.CampaignName,
            Version = encounter.Version,
            State = CmJson.ReadEncounterState(encounter.State, encounter.StateVersion),
            UpdatedAt = encounter.UpdatedAt,
        };
    }

    /// <summary>
    /// Состояние целиком, с версией, которую правили. Завершённую сцену не пишут (409): её вторая вкладка не должна
    /// воскрешать. Пределы — размер документа и число участников; правила сцены исполняет Core на клиенте.
    /// </summary>
    public async Task<EncounterSavedDto> SaveStateAsync(Guid encounterId, EncounterState state, uint? ifMatch, CancellationToken cancellationToken)
    {
        await access.ForEncounterAsync(encounterId, cancellationToken).Demand(Operation.Edit);
        Validate(state);

        return await WriteAsync(encounterId, ifMatch, encounter =>
        {
            if (encounter.Status != EncounterStatus.Active)
            {
                throw ApiProblemException.Conflict("Сцена уже завершена — начните новую.");
            }

            encounter.State = CmJson.Write(state);
            encounter.StateVersion = EncounterState.CurrentVersion;
        }, cancellationToken);
    }

    /// <summary>Завершить: статус <c>Finished</c>, место под новую сцену этого вида освобождается. Повтор — без ошибки.</summary>
    public async Task<EncounterSavedDto> FinishAsync(Guid encounterId, uint? ifMatch, CancellationToken cancellationToken)
    {
        await access.ForEncounterAsync(encounterId, cancellationToken).Demand(Operation.Edit);
        return await WriteAsync(encounterId, ifMatch, encounter => encounter.Status = EncounterStatus.Finished, cancellationToken);
    }

    private async Task<EncounterSavedDto> WriteAsync(Guid encounterId, uint? ifMatch, Action<Encounter> change, CancellationToken cancellationToken)
    {
        if (ifMatch is not { } version)
        {
            throw ApiProblemException.VersionRequired();
        }

        var encounter = await dbContext.Encounters.SingleAsync(e => e.Id == encounterId, cancellationToken);
        if (encounter.Version != version)
        {
            throw ApiProblemException.Stale();
        }

        // Версия, с которой сравнивает сама запись (xmin): между чтением и UPDATE сцену могла записать другая вкладка.
        dbContext.Entry(encounter).Property(e => e.Version).OriginalValue = version;
        change(encounter);
        await dbContext.SaveChangesAsync(cancellationToken);
        return new EncounterSavedDto(encounter.Version, encounter.UpdatedAt);
    }

    private static void Validate(EncounterState state)
    {
        if (state.Participants.Count > EncounterEngine.MaxParticipants)
        {
            throw ApiProblemException.Invalid($"В сцене не больше {EncounterEngine.MaxParticipants} участников.");
        }

        if (state.Log.Count > EncounterEngine.MaxLogEntries)
        {
            throw ApiProblemException.Invalid($"Журнал сцены — не больше {EncounterEngine.MaxLogEntries} записей.");
        }

        if (state.Participants.Select(p => p.Id).Distinct().Count() != state.Participants.Count)
        {
            throw ApiProblemException.Invalid("Два участника с одним id — перечитайте сцену.");
        }

        var sheets = state.Participants.Select(p => p.SourceCharacterId).OfType<Guid>().ToList();
        if (sheets.Distinct().Count() != sheets.Count)
        {
            throw ApiProblemException.Invalid("Один лист в сцене дважды: один лист — один участник.");
        }

        // Погоня (T2.6c): трасса в пределах, каждый бегущий — участник сцены, один раз.
        if (state.Chase is { } chase)
        {
            if (chase.Locations.Count > ChaseRules.MaxLocations)
            {
                throw ApiProblemException.Invalid($"На трассе не больше {ChaseRules.MaxLocations} локаций.");
            }

            var ids = state.Participants.Select(p => p.Id).ToHashSet();
            if (chase.Runners.Any(r => !ids.Contains(r.ParticipantId))
                || chase.Runners.Select(r => r.ParticipantId).Distinct().Count() != chase.Runners.Count)
            {
                throw ApiProblemException.Invalid("В погоне бегущий без участника сцены — перечитайте сцену.");
            }
        }

        if (Encoding.UTF8.GetByteCount(CmJson.Serialize(state)) > EncounterLimits.MaxStateBytes)
        {
            throw ApiProblemException.Invalid("Состояние сцены слишком велико.");
        }
    }
}
