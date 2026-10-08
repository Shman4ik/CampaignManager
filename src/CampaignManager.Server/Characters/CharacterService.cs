using CampaignManager.Contracts.Characters;
using CampaignManager.Contracts.Files;
using CampaignManager.Core;

using CampaignManager.Core.Characters;
using CampaignManager.Core.Documents;
using CampaignManager.Data;
using CampaignManager.Data.Characters;
using CampaignManager.Server.Access;
using CampaignManager.Server.Campaigns;
using CampaignManager.Server.Platform;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CampaignManager.Server.Characters;

/// <summary>
/// Лист сыщика: чтение, запись документа, портрет, статус, соседи по столу. Права — <see cref="AccessPolicy.ForCharacterAsync"/>
/// в каждом методе (владелец-игрок и Хранитель его кампании; «нет листа» и «нет доступа» — одинаковый 404).
/// <para>
/// Каждая запись — с версией, которую правили (<c>If-Match</c>): лист правят Хранитель и игрок с разных
/// устройств, и в v1 автосохранение одной вкладки молча затирало другую (AUDIT, «Персонажи и НПС → Ошибки», 8).
/// Устаревшая версия — 409 <c>stale</c> до записи, гонка — <c>xmin</c> в самой записи (тоже 409).
/// </para>
/// </summary>
public sealed class CharacterService(CmDbContext dbContext, AccessPolicy access, CurrentUser currentUser, ILogger<CharacterService> logger)
{
    public async Task<CharacterDto> GetAsync(Guid characterId, CancellationToken cancellationToken)
    {
        var rights = await access.ForCharacterAsync(characterId, cancellationToken).Demand(Operation.Read);
        var row = await dbContext.Characters.AsNoTracking()
            .Where(c => c.Id == characterId)
            .Select(c => new
            {
                Character = c,
                CampaignName = dbContext.Campaigns.Where(x => x.Id == c.CampaignId).Select(x => x.Name).FirstOrDefault(),
                Era = dbContext.Campaigns.Where(x => x.Id == c.CampaignId).Select(x => (Era?)x.Era).FirstOrDefault(),
                ScenarioName = dbContext.Scenarios.Where(x => x.Id == c.ScenarioId).Select(x => x.Name).FirstOrDefault(),
                Alias = dbContext.CampaignMembers.Where(m => m.CampaignId == c.CampaignId && m.UserId == c.OwnerId)
                    .Select(m => m.DisplayName).FirstOrDefault(),
                OwnerName = dbContext.Users.Where(u => u.Id == c.OwnerId).Select(u => u.DisplayName).FirstOrDefault(),
            })
            .SingleAsync(cancellationToken);

        var character = row.Character;
        var me = await currentUser.GetAsync(cancellationToken);
        return new CharacterDto
        {
            Id = character.Id,
            Kind = character.Kind,
            Status = character.Status,
            Version = character.Version,
            Sheet = CmJson.ReadSheet(character.Sheet, character.SheetVersion),
            PortraitFileId = character.PortraitFileId,
            PortraitUrl = character.PortraitFileId is { } file ? FilesRoutes.Content(file) : null,
            PlayerName = character.OwnerId is null ? null : PublicNames.Of(row.Alias, row.OwnerName),
            CampaignId = character.CampaignId,
            CampaignName = row.CampaignName,
            Era = row.Era ?? Era.Classic,
            ScenarioId = character.ScenarioId,
            ScenarioName = row.ScenarioName,
            CanEdit = rights.CanEdit,
            CanDelete = rights.CanDelete,
            IsMine = me is not null && character.OwnerId == me.Id,
            UpdatedAt = character.UpdatedAt,
        };
    }

    /// <summary>
    /// Документ целиком. В нём только то, что вводит человек (максимумы, БкУ и прочее вычисляет Core), а игрок,
    /// портрет и владелец — колонки строки: запись листа их не трогает.
    /// </summary>
    public async Task<CharacterSavedDto> SaveSheetAsync(Guid characterId, CharacterSheet sheet, uint? ifMatch, CancellationToken cancellationToken)
    {
        await access.ForCharacterAsync(characterId, cancellationToken).Demand(Operation.Edit);
        await ValidateAsync(dbContext, sheet, cancellationToken);

        return await WriteAsync(characterId, ifMatch, character =>
        {
            character.Sheet = CmJson.Write(sheet);
            character.SheetVersion = CharacterSheet.CurrentVersion;
            return Task.CompletedTask;
        }, cancellationToken);
    }

    /// <summary>Портрет — строка <c>files</c> (загрузил модуль Files); null — убрать. Сам файл не удаляется: сироты — админке.</summary>
    public async Task<CharacterSavedDto> SetPortraitAsync(Guid characterId, Guid? fileId, uint? ifMatch, CancellationToken cancellationToken)
    {
        await access.ForCharacterAsync(characterId, cancellationToken).Demand(Operation.Edit);
        if (fileId is { } id && !await dbContext.Files.AnyAsync(f => f.Id == id, cancellationToken))
        {
            throw ApiProblemException.Invalid("Файл портрета не найден — загрузите его заново.");
        }

        return await WriteAsync(characterId, ifMatch, character =>
        {
            character.PortraitFileId = fileId;
            return Task.CompletedTask;
        }, cancellationToken);
    }

    /// <summary>
    /// Статус листа. У игрока в кампании активный лист один (частичный уникальный индекс) — второй активный
    /// получает 409 с объяснением, а не исключение базы.
    /// </summary>
    public async Task<CharacterSavedDto> SetStatusAsync(Guid characterId, CharacterStatus status, uint? ifMatch, CancellationToken cancellationToken)
    {
        await access.ForCharacterAsync(characterId, cancellationToken).Demand(Operation.Edit);
        if (!Enum.IsDefined(status))
        {
            throw ApiProblemException.Invalid("Неизвестный статус листа.");
        }

        try
        {
            return await WriteAsync(characterId, ifMatch, character =>
            {
                character.Status = status;
                return Task.CompletedTask;
            }, cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            throw ApiProblemException.Conflict("У игрока в этой кампании уже есть активный лист — сначала смените его статус.");
        }
    }

    /// <summary>
    /// Соседи по столу для «Знакомых сыщиков»: у сыщика — активные листы игроков той же кампании, у прегена —
    /// прегены того же сценария. Имя и профессия — generated-колонки, документ не читается; почты нет.
    /// </summary>
    public async Task<IReadOnlyList<PartyMemberDto>> GetPartyAsync(Guid characterId, CancellationToken cancellationToken)
    {
        await access.ForCharacterAsync(characterId, cancellationToken).Demand(Operation.Read);
        var self = await dbContext.Characters.AsNoTracking()
            .Where(c => c.Id == characterId)
            .Select(c => new { c.Kind, c.CampaignId, c.ScenarioId })
            .SingleAsync(cancellationToken);

        IQueryable<Character> neighbours = self switch
        {
            { Kind: CharacterKind.Player, CampaignId: { } campaignId } => dbContext.Characters.Where(c =>
                c.CampaignId == campaignId && c.Kind == CharacterKind.Player && c.Status == CharacterStatus.Active),
            { Kind: CharacterKind.Pregen, ScenarioId: { } scenarioId } => dbContext.Characters.Where(c =>
                c.ScenarioId == scenarioId && c.Kind == CharacterKind.Pregen && c.Status != CharacterStatus.Archived),
            _ => dbContext.Characters.Where(_ => false),
        };

        var rows = await neighbours
            .Where(c => c.Id != characterId)
            .Select(c => new
            {
                c.Id,
                c.Name,
                c.Occupation,
                Alias = dbContext.CampaignMembers.Where(m => m.CampaignId == c.CampaignId && m.UserId == c.OwnerId)
                    .Select(m => m.DisplayName).FirstOrDefault(),
                OwnerName = dbContext.Users.Where(u => u.Id == c.OwnerId).Select(u => u.DisplayName).FirstOrDefault(),
            })
            .ToListAsync(cancellationToken);

        return
        [
            .. rows
                .Select(r => new PartyMemberDto(r.Id, r.Name ?? "", string.IsNullOrWhiteSpace(r.Occupation) ? null : r.Occupation,
                    r.OwnerName is null ? null : PublicNames.Of(r.Alias, r.OwnerName)))
                .OrderBy(p => p.Name, StringComparer.CurrentCulture),
        ];
    }

    /// <summary>
    /// Активные сыщики кампании с листами — для проверок Хранителя (групповая проверка ширмы). Только тому,
    /// кто правит кампанию: игрок чужих листов не видит.
    /// </summary>
    public async Task<IReadOnlyList<InvestigatorDto>> GetCampaignInvestigatorsAsync(Guid campaignId, CancellationToken cancellationToken)
    {
        await access.ForCampaignAsync(campaignId, cancellationToken).Demand(Operation.Edit);
        var rows = await dbContext.Characters.AsNoTracking()
            .Where(c => c.CampaignId == campaignId && c.Kind == CharacterKind.Player && c.Status == CharacterStatus.Active)
            .Select(c => new
            {
                c.Id,
                c.Sheet,
                c.SheetVersion,
                Alias = dbContext.CampaignMembers.Where(m => m.CampaignId == c.CampaignId && m.UserId == c.OwnerId)
                    .Select(m => m.DisplayName).FirstOrDefault(),
                OwnerName = dbContext.Users.Where(u => u.Id == c.OwnerId).Select(u => u.DisplayName).FirstOrDefault(),
            })
            .ToListAsync(cancellationToken);

        return
        [
            .. rows
                .Select(r =>
                {
                    var sheet = CmJson.ReadSheet(r.Sheet, r.SheetVersion);
                    return new InvestigatorDto(r.Id, sheet.Personal.Name, PublicNames.Of(r.Alias, r.OwnerName), sheet);
                })
                .OrderBy(i => i.Name, StringComparer.CurrentCulture),
        ];
    }

    private async Task<CharacterSavedDto> WriteAsync(Guid characterId, uint? ifMatch, Func<Character, Task> change, CancellationToken cancellationToken)
    {
        if (ifMatch is not { } version)
        {
            throw ApiProblemException.VersionRequired();
        }

        var character = await dbContext.Characters.SingleAsync(c => c.Id == characterId, cancellationToken);
        if (character.Version != version)
        {
            throw ApiProblemException.Stale();
        }

        // Версия, с которой сравнивает сама запись (xmin): между чтением и UPDATE лист мог записать другой запрос.
        dbContext.Entry(character).Property(c => c.Version).OriginalValue = version;
        await change(character);
        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Лист {CharacterId} записан, версия {Version}", characterId, character.Version);
        return new CharacterSavedDto(character.Version, character.UpdatedAt);
    }

    /// <summary>
    /// Документ от клиента: пределы длины и ссылки на справочник навыков. Неизвестный навык — 400: лист без
    /// справочника не читается (имя, группа и база берутся из него).
    /// </summary>
    internal static async Task ValidateAsync(CmDbContext dbContext, CharacterSheet sheet, CancellationToken cancellationToken)
    {
        if (sheet.Personal.Name.Length > CharacterLimits.NameLength || sheet.Personal.Occupation.Length > CharacterLimits.NameLength)
        {
            throw ApiProblemException.Invalid($"Имя и профессия — не длиннее {CharacterLimits.NameLength} знаков.");
        }

        var b = sheet.Biography;
        string[] texts =
        [
            b.Appearance, b.Traits, b.IdealsAndPrinciples, b.SignificantPeople, b.ImportantPlaces, b.ValuablePossessions,
            b.SupernaturalEncounters, b.Injuries, b.Backstory, b.KeyConnection, b.Notes, sheet.Finances.Note, sheet.Finances.Assets,
        ];
        if (texts.Any(t => t.Length > CharacterLimits.TextLength))
        {
            throw ApiProblemException.Invalid($"Графа биографии — не длиннее {CharacterLimits.TextLength} знаков.");
        }

        if (sheet.Skills.Count > CharacterLimits.MaxListItems || sheet.Weapons.Count > CharacterLimits.MaxListItems
            || sheet.Spells.Count > CharacterLimits.MaxListItems || sheet.Equipment.Count > CharacterLimits.MaxListItems)
        {
            throw ApiProblemException.Invalid("Слишком длинный список в листе.");
        }

        var skillIds = sheet.Skills.Select(s => s.SkillId).Concat(sheet.Skills.Select(s => s.ParentSkillId))
            .Concat(sheet.Weapons.Select(w => w.SkillId))
            .OfType<Guid>().Distinct().ToList();
        if (skillIds.Count > 0)
        {
            var known = await dbContext.Skills.CountAsync(s => skillIds.Contains(s.Id), cancellationToken);
            if (known != skillIds.Count)
            {
                throw ApiProblemException.Invalid("В листе навык, которого нет в справочнике. Перечитайте лист.");
            }
        }

        if (sheet.Skills.Any(s => s.SkillId is null && string.IsNullOrWhiteSpace(s.Name)))
        {
            throw ApiProblemException.Invalid("У своего навыка нет имени.");
        }
    }
}
