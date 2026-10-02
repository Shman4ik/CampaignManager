using CampaignManager.Contracts.Characters;
using CampaignManager.Contracts.Files;
using CampaignManager.Core;
using CampaignManager.Core.Campaigns;
using CampaignManager.Core.Characters;
using CampaignManager.Core.Documents;
using CampaignManager.Data;
using CampaignManager.Data.Characters;
using CampaignManager.Data.Scenarios;
using CampaignManager.Server.Access;
using CampaignManager.Server.Platform;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CampaignManager.Server.Characters;

/// <summary>
/// Создание листов (T2.4) и библиотека НПС и прегенов. Документ нового листа собирает клиент правилом Core
/// (<c>SheetBuilder</c>), сервер проверяет его тем же <see cref="CharacterService.ValidateAsync"/>, что и запись листа, и
/// решает, чей он: владельца по виду — <see cref="AccessPolicy.CanCreateCharacterAsync"/>, место в сценарии —
/// <see cref="AccessPolicy.ForScenarioAsync"/>.
/// </summary>
public sealed class CharacterLibraryService(
    CmDbContext dbContext,
    AccessPolicy access,
    CurrentUser currentUser,
    ILogger<CharacterLibraryService> logger)
{
    private const string OneActiveSheet =
        "У вас уже есть активный сыщик в этой кампании — сначала смените его статус на листе (выбыл, в отставке).";

    /// <summary>Новый лист. Лист и связь НПС со сценарием — одной записью: либо оба, либо ничего.</summary>
    public async Task<CharacterCreatedDto> CreateAsync(CreateCharacterRequest request, CancellationToken cancellationToken)
    {
        if (!Enum.IsDefined(request.Kind))
        {
            throw ApiProblemException.Invalid("Неизвестный вид листа.");
        }

        await access.CanCreateCharacterAsync(request.Kind, request.CampaignId, cancellationToken).Demand();
        var user = await currentUser.GetAsync(cancellationToken) ?? throw AccessDeniedException.Forbidden();

        switch (request.Kind)
        {
            case CharacterKind.Player when request.ScenarioId is not null || request.Cast is not null:
            case CharacterKind.Pregen when request.CampaignId is not null || request.Cast is not null:
            case CharacterKind.Npc when request.ScenarioId is not null:
                throw ApiProblemException.Invalid("Такого места для листа этого вида нет.");
        }

        if (request.Kind is CharacterKind.Pregen && request.ScenarioId is { } scenarioId)
        {
            await access.ForScenarioAsync(scenarioId, cancellationToken).Demand(Operation.Edit);
        }

        if (request.Cast is { } cast)
        {
            await access.ForScenarioAsync(cast.ScenarioId, cancellationToken).Demand(Operation.Edit);
            if (!Enum.IsDefined(cast.Role) || cast.Count is < 1 or > CharacterLimits.MaxCastCount)
            {
                throw ApiProblemException.Invalid($"Роль НПС и количество (1–{CharacterLimits.MaxCastCount}) — как в сценарии.");
            }
        }

        if (request.Kind is CharacterKind.Player && request.CampaignId is { } campaignId
                                                 && await ActiveSheetAsync(campaignId, user.Id, cancellationToken) is not null)
        {
            throw ApiProblemException.Conflict(OneActiveSheet);
        }

        await CharacterService.ValidateAsync(dbContext, request.Sheet, cancellationToken);
        if (string.IsNullOrWhiteSpace(request.Sheet.Personal.Name) && request.Kind is not CharacterKind.Player)
        {
            throw ApiProblemException.Invalid("У НПС и прегена должно быть имя — по нему их ищут в библиотеке.");
        }

        var character = new Character
        {
            Kind = request.Kind,
            Status = CharacterStatus.Active,
            OwnerId = request.Kind is CharacterKind.Player ? user.Id : null,
            CampaignId = request.Kind is CharacterKind.Pregen ? null : request.CampaignId,
            ScenarioId = request.Kind is CharacterKind.Pregen ? request.ScenarioId : null,
            Sheet = CmJson.Write(request.Sheet),
            SheetVersion = CharacterSheet.CurrentVersion,
            CreatedById = user.Id,
        };
        dbContext.Characters.Add(character);

        if (request.Cast is { } castRow)
        {
            dbContext.ScenarioNpcs.Add(new ScenarioNpc
            {
                ScenarioId = castRow.ScenarioId,
                CharacterId = character.Id,
                Role = castRow.Role,
                Count = castRow.Count,
                Notes = string.IsNullOrWhiteSpace(castRow.Notes) ? null : castRow.Notes.Trim(),
            });
        }

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            throw ApiProblemException.Conflict(OneActiveSheet);
        }

        logger.LogInformation("Создан лист {CharacterId} ({Kind})", character.Id, character.Kind);
        return new CharacterCreatedDto(character.Id, character.Version);
    }

    /// <summary>
    /// Куда ляжет новый лист и можно ли его завести — до первого шага помощника. Название кампании и сценария — только
    /// тому, кто их видит.
    /// </summary>
    public async Task<CreationContextDto> GetCreationContextAsync(CharacterKind kind, Guid? campaignId, Guid? scenarioId,
        CancellationToken cancellationToken)
    {
        if (!Enum.IsDefined(kind))
        {
            throw ApiProblemException.Invalid("Неизвестный вид листа.");
        }

        var user = await currentUser.GetAsync(cancellationToken) ?? throw AccessDeniedException.Forbidden();
        var context = new CreationContextDto { Kind = kind, CampaignId = campaignId, ScenarioId = scenarioId };

        if (campaignId is { } campaign && (await access.ForCampaignAsync(campaign, cancellationToken)).CanRead)
        {
            var row = await dbContext.Campaigns.AsNoTracking().Where(c => c.Id == campaign)
                .Select(c => new { c.Name, c.Era }).SingleAsync(cancellationToken);
            context.CampaignName = row.Name;
            context.Era = row.Era;
        }

        if (scenarioId is { } scenario && (await access.ForScenarioAsync(scenario, cancellationToken)).CanRead)
        {
            var row = await dbContext.Scenarios.AsNoTracking().Where(s => s.Id == scenario)
                .Select(s => new { s.Name, s.Era }).SingleAsync(cancellationToken);
            context.ScenarioName = row.Name;
            context.Era = campaignId is null || context.CampaignName is null ? row.Era ?? Era.Classic : context.Era;
        }

        var placeCampaign = kind is CharacterKind.Pregen ? null : campaignId;
        if (!await access.CanCreateCharacterAsync(kind, placeCampaign, cancellationToken))
        {
            context.Reason = kind switch
            {
                CharacterKind.Player => "Сыщика заводят в кампании, где вы участник: вступите в неё с главной.",
                CharacterKind.Npc when campaignId is not null => "НПС кампании заводит её Хранитель.",
                _ => "НПС и прегенов заводит Хранитель.",
            };
            return context;
        }

        if (scenarioId is not null && context.ScenarioName is null)
        {
            context.Reason = "Сценарий не найден.";
            return context;
        }

        if (kind is CharacterKind.Player && campaignId is { } playerCampaign
                                         && await ActiveSheetAsync(playerCampaign, user.Id, cancellationToken) is { } active)
        {
            context.ActiveCharacterId = active;
            context.Reason = OneActiveSheet;
            return context;
        }

        context.CanCreate = true;
        return context;
    }

    /// <summary>
    /// Библиотека: НПС (из библиотеки и кампаний, которые ведёт Хранитель; администратору — все) или прегены, без архива
    /// или только архив. Флаги — тем же правилом, что у листа (<see cref="AccessPolicy.ForCharacter"/>).
    /// </summary>
    public async Task<IReadOnlyList<CharacterSummaryDto>> ListAsync(CharacterKind kind, bool archived, CancellationToken cancellationToken)
    {
        await access.CanBrowseCharacterLibraryAsync(cancellationToken).Demand();
        if (kind is not (CharacterKind.Npc or CharacterKind.Pregen))
        {
            throw ApiProblemException.Invalid("В библиотеке — НПС и прегены.");
        }

        var user = await currentUser.GetAsync(cancellationToken) ?? throw AccessDeniedException.Forbidden();
        var query = dbContext.Characters.Where(c =>
            c.Kind == kind && (archived ? c.Status == CharacterStatus.Archived : c.Status != CharacterStatus.Archived));
        return await CharacterSummaries.ReadAsync(dbContext, query, user, cancellationToken);
    }

    private Task<Guid?> ActiveSheetAsync(Guid campaignId, Guid userId, CancellationToken cancellationToken) =>
        dbContext.Characters
            .Where(c => c.CampaignId == campaignId && c.OwnerId == userId && c.Kind == CharacterKind.Player
                        && c.Status == CharacterStatus.Active)
            .Select(c => (Guid?)c.Id)
            .FirstOrDefaultAsync(cancellationToken);
}
