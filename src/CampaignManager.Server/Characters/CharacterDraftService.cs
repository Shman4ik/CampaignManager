using System.Text.Json;
using CampaignManager.Contracts.Characters;
using CampaignManager.Core.Characters;
using CampaignManager.Core.Documents;
using CampaignManager.Data;
using CampaignManager.Data.Characters;
using CampaignManager.Server.Access;
using CampaignManager.Server.Platform;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CampaignManager.Server.Characters;

/// <summary>
/// Черновик помощника создания сыщика игрока в кампании (<c>character_drafts</c>): продолжить с любого устройства, Хранитель
/// видит шаг. Права — <see cref="AccessPolicy.ForCharacterDraftAsync"/> (пишет только сам игрок), запись — с версией, как у листа:
/// первая без <c>If-Match</c>, дальше устаревшая версия — 409 <c>stale</c>, а не молчаливая перезапись с другого устройства.
/// Черновик стирают создание листа (<see cref="CharacterLibraryService.CreateAsync"/>), «Начать заново» и выход из кампании
/// (каскад от <c>campaign_members</c>).
/// </summary>
public sealed class CharacterDraftService(
    CmDbContext dbContext,
    AccessPolicy access,
    CurrentUser currentUser,
    ILogger<CharacterDraftService> logger)
{
    /// <summary>Предел документа черновика в знаках JSON: шесть граф биографии с запасом.</summary>
    public const int MaxDraftBytes = 256 * 1024;

    public async Task<CharacterDraftDto> GetMineAsync(Guid campaignId, CancellationToken cancellationToken)
    {
        var user = await currentUser.GetAsync(cancellationToken) ?? throw AccessDeniedException.NotFound();
        return await GetAsync(campaignId, user.Id, cancellationToken);
    }

    public async Task<CharacterDraftDto> GetAsync(Guid campaignId, Guid ownerId, CancellationToken cancellationToken)
    {
        await access.ForCharacterDraftAsync(campaignId, ownerId, cancellationToken).Demand(Operation.Read);
        var row = await dbContext.CharacterDrafts.AsNoTracking()
            .SingleAsync(d => d.CampaignId == campaignId && d.OwnerId == ownerId, cancellationToken);
        return new CharacterDraftDto(row.CampaignId, row.OwnerId, CmJson.ReadDraft(row.Draft, row.DraftVersion), row.Version, row.UpdatedAt);
    }

    /// <summary>
    /// Мой черновик целиком. Нет на сервере и версии нет — новый; нет, а версия пришла — его стёрли с другого устройства
    /// (создали лист или начали заново): 409 <c>stale</c>, клиент решает, записать ли свой заново.
    /// </summary>
    public async Task<CharacterSavedDto> SaveMineAsync(Guid campaignId, InvestigatorDraft draft, uint? ifMatch, CancellationToken cancellationToken)
    {
        await access.CanWriteCharacterDraftAsync(campaignId, cancellationToken).Demand();
        var user = await currentUser.GetAsync(cancellationToken) ?? throw AccessDeniedException.Forbidden();
        var document = Validate(draft);

        var row = await dbContext.CharacterDrafts.SingleOrDefaultAsync(d => d.CampaignId == campaignId && d.OwnerId == user.Id, cancellationToken);
        if (row is null)
        {
            if (ifMatch is not null)
            {
                throw ApiProblemException.Stale();
            }

            row = new CharacterDraft { CampaignId = campaignId, OwnerId = user.Id, Draft = document };
            dbContext.CharacterDrafts.Add(row);
        }
        else
        {
            if (ifMatch is not { } version)
            {
                throw ApiProblemException.VersionRequired();
            }

            if (row.Version != version)
            {
                throw ApiProblemException.Stale();
            }

            // Версия, с которой сравнивает сама запись (xmin): между чтением и UPDATE черновик мог записать другой запрос.
            dbContext.Entry(row).Property(d => d.Version).OriginalValue = version;
            row.Draft = document;
        }

        row.DraftVersion = InvestigatorDraft.CurrentVersion;
        row.Step = draft.StepIndex;

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // Два устройства завели черновик одновременно: второй — как устаревшая запись.
            throw ApiProblemException.Stale();
        }

        logger.LogInformation("Черновик сыщика {OwnerId} в кампании {CampaignId} записан, шаг {Step}", user.Id, campaignId, row.Step);
        return new CharacterSavedDto(row.Version, row.UpdatedAt);
    }

    /// <summary>«Начать заново»: стереть мой черновик. Его нет — тоже успех (стёрли с другого устройства или ещё не писали).</summary>
    public async Task DeleteMineAsync(Guid campaignId, CancellationToken cancellationToken)
    {
        var user = await currentUser.GetAsync(cancellationToken) ?? throw AccessDeniedException.NotFound();
        var rights = await access.ForCharacterDraftAsync(campaignId, user.Id, cancellationToken);
        if (!rights.CanRead)
        {
            return;
        }

        rights.Demand(Operation.Delete);
        await dbContext.CharacterDrafts
            .Where(d => d.CampaignId == campaignId && d.OwnerId == user.Id)
            .ExecuteDeleteAsync(cancellationToken);
        logger.LogInformation("Черновик сыщика {OwnerId} в кампании {CampaignId} стёрт", user.Id, campaignId);
    }

    /// <summary>Шаг — один из семи, имя и размер — в пределах листа: черновик потом становится листом тем же помощником.</summary>
    private static JsonDocument Validate(InvestigatorDraft draft)
    {
        if (draft.StepIndex is < (int)CreationStep.Method or > (int)CreationStep.Summary)
        {
            throw ApiProblemException.Invalid("Неизвестный шаг помощника.");
        }

        if (draft.Personal is null)
        {
            throw ApiProblemException.Invalid("В черновике нет анкеты.");
        }

        if (draft.Personal.Name.Length > CharacterLimits.NameLength)
        {
            throw ApiProblemException.Invalid($"Имя — не длиннее {CharacterLimits.NameLength} знаков.");
        }

        var document = CmJson.Write(draft);
        if (document.RootElement.GetRawText().Length > MaxDraftBytes)
        {
            document.Dispose();
            throw ApiProblemException.Invalid("Черновик слишком большой.");
        }

        return document;
    }
}
