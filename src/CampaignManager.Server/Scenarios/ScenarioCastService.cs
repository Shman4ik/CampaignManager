using CampaignManager.Contracts.Characters;
using CampaignManager.Contracts.Scenarios;
using CampaignManager.Core.Characters;
using CampaignManager.Data;
using CampaignManager.Data.Characters;
using CampaignManager.Data.Scenarios;
using CampaignManager.Server.Access;
using CampaignManager.Server.Platform;
using Microsoft.EntityFrameworkCore;

namespace CampaignManager.Server.Scenarios;

/// <summary>
/// Состав НПС и прегены сценария. НПС не копируется — он занят связью <c>scenario_npcs</c> с ролью и количеством, и
/// правка листа видна во всех сценариях. Преген — лист сценария (<c>characters.scenario_id</c>): из библиотеки он
/// приходит копией, потому что бронь (T2.5c) копирует уже прегена сценария. Новые листы из сценария пишет модуль
/// Characters (<c>POST /characters</c> с <c>Cast</c> или <c>scenarioId</c>).
/// </summary>
public sealed class ScenarioCastService(CmDbContext dbContext, AccessPolicy access, CurrentUser currentUser, ILogger<ScenarioCastService> logger)
{
    /// <summary>
    /// Занять НПС в сценарии или поменять роль, количество и заметку. Повтор — правка той же строки, двойника нет.
    /// НПС — лист вида <c>Npc</c>, который пользователь видит (НПС чужой кампании — 404, как и несуществующий).
    /// </summary>
    public async Task CastNpcAsync(Guid scenarioId, Guid characterId, NpcCastInput input, CancellationToken cancellationToken)
    {
        await access.ForScenarioAsync(scenarioId, cancellationToken).Demand(Operation.Edit);
        await access.ForCharacterAsync(characterId, cancellationToken).Demand(Operation.Read);
        if (!Enum.IsDefined(input.Role) || input.Count is < 1 or > CharacterLimits.MaxCastCount)
        {
            throw ApiProblemException.Invalid($"Роль НПС и количество (1–{CharacterLimits.MaxCastCount}) — как в сценарии.");
        }

        var kind = await dbContext.Characters.Where(c => c.Id == characterId).Select(c => c.Kind).SingleAsync(cancellationToken);
        if (kind is not CharacterKind.Npc)
        {
            throw ApiProblemException.Invalid("В состав сценария занимают НПС; готовые сыщики у сценария свои.");
        }

        var row = await dbContext.ScenarioNpcs.SingleOrDefaultAsync(n => n.ScenarioId == scenarioId && n.CharacterId == characterId,
            cancellationToken);
        if (row is null)
        {
            row = new ScenarioNpc { ScenarioId = scenarioId, CharacterId = characterId };
            dbContext.ScenarioNpcs.Add(row);
        }

        row.Role = input.Role;
        row.Count = input.Count;
        row.Notes = ScenarioService.Text(input.Notes, ScenarioLimits.TextLength, "Заметка");
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Убрать НПС из состава — только связь: лист остаётся в библиотеке (в v1 отвязка прятала его навсегда).</summary>
    public async Task RemoveNpcAsync(Guid scenarioId, Guid characterId, CancellationToken cancellationToken)
    {
        await access.ForScenarioAsync(scenarioId, cancellationToken).Demand(Operation.Edit);
        if (await dbContext.ScenarioNpcs.Where(n => n.ScenarioId == scenarioId && n.CharacterId == characterId)
                .ExecuteDeleteAsync(cancellationToken) == 0)
        {
            throw AccessDeniedException.NotFound();
        }
    }

    /// <summary>
    /// Преген из библиотеки (без сценария) — копией в сценарий: заготовка остаётся для других. Портрет и документ —
    /// те же, лист новый (<c>origin_character_id</c> — у брони, здесь не ставится: это не бронь).
    /// </summary>
    public async Task<CharacterCreatedDto> AddPregenAsync(Guid scenarioId, Guid pregenId, CancellationToken cancellationToken)
    {
        await access.ForScenarioAsync(scenarioId, cancellationToken).Demand(Operation.Edit);
        await access.ForCharacterAsync(pregenId, cancellationToken).Demand(Operation.Read);
        var user = await currentUser.GetAsync(cancellationToken) ?? throw AccessDeniedException.Forbidden();

        var source = await dbContext.Characters.AsNoTracking().SingleAsync(c => c.Id == pregenId, cancellationToken);
        if (source.Kind is not CharacterKind.Pregen || source.ScenarioId is not null)
        {
            throw ApiProblemException.Invalid("Копировать в сценарий можно готового сыщика из библиотеки.");
        }

        var copy = new Character
        {
            Kind = CharacterKind.Pregen,
            Status = CharacterStatus.Active,
            ScenarioId = scenarioId,
            PortraitFileId = source.PortraitFileId,
            Sheet = source.Sheet,
            SheetVersion = source.SheetVersion,
            CreatedById = user.Id,
        };
        dbContext.Characters.Add(copy);
        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Преген {PregenId} скопирован в сценарий {ScenarioId} как {CharacterId}", pregenId, scenarioId, copy.Id);
        return new CharacterCreatedDto(copy.Id, copy.Version);
    }

    /// <summary>
    /// Убрать прегена из сценария: он уходит в архив библиотеки (не удаляется — как в v1). Забронированного в
    /// незавершённом прохождении — 409: сначала снимают бронь.
    /// </summary>
    public async Task RemovePregenAsync(Guid scenarioId, Guid characterId, CancellationToken cancellationToken)
    {
        await access.ForScenarioAsync(scenarioId, cancellationToken).Demand(Operation.Edit);
        var pregen = await dbContext.Characters
                         .SingleOrDefaultAsync(c => c.Id == characterId && c.ScenarioId == scenarioId && c.Kind == CharacterKind.Pregen,
                             cancellationToken)
                     ?? throw AccessDeniedException.NotFound();

        if ((await ScenarioService.ReservedPregensAsync(dbContext, scenarioId, cancellationToken)).Contains(characterId))
        {
            throw ApiProblemException.Conflict("Готового сыщика занял игрок — сначала снимите запись.");
        }

        pregen.ScenarioId = null;
        pregen.Status = CharacterStatus.Archived;
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
