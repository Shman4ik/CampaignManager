using CampaignManager.Core;
using CampaignManager.Core.Characters;

namespace CampaignManager.Contracts.Characters;

/// <summary>Лист сыщика: чтение, запись документа с <c>If-Match</c>, портрет, статус, соседи по столу.</summary>
public static class CharactersRoutes
{
    public const string Characters = ApiRoutes.Prefix + "/characters";

    /// <summary><c>GET</c> — <see cref="CharacterDto"/> и <c>ETag</c> с версией.</summary>
    public const string CharacterPattern = Characters + "/{characterId:guid}";

    /// <summary><c>PUT</c> <see cref="CharacterSheet"/> с <c>If-Match: "версия"</c> → <see cref="CharacterSavedDto"/>.</summary>
    public const string SheetPattern = CharacterPattern + "/sheet";

    /// <summary><c>PUT</c> <see cref="SetPortraitRequest"/> с <c>If-Match</c>.</summary>
    public const string PortraitPattern = CharacterPattern + "/portrait";

    /// <summary><c>PUT</c> <see cref="SetStatusRequest"/> с <c>If-Match</c>.</summary>
    public const string StatusPattern = CharacterPattern + "/status";

    /// <summary><c>GET</c> — соседи по столу для «Знакомых сыщиков»: <see cref="PartyMemberDto"/>.</summary>
    public const string PartyPattern = CharacterPattern + "/party";

    /// <summary><c>GET</c> — активные сыщики кампании с листами (для проверок Хранителя): <see cref="InvestigatorDto"/>.</summary>
    public const string CampaignInvestigatorsPattern = ApiRoutes.Prefix + "/campaigns/{campaignId:guid}/investigators";

    public static string Character(Guid characterId) => $"{Characters}/{characterId}";

    public static string Sheet(Guid characterId) => $"{Character(characterId)}/sheet";

    public static string Portrait(Guid characterId) => $"{Character(characterId)}/portrait";

    public static string Status(Guid characterId) => $"{Character(characterId)}/status";

    public static string Party(Guid characterId) => $"{Character(characterId)}/party";

    public static string CampaignInvestigators(Guid campaignId) => $"{ApiRoutes.Prefix}/campaigns/{campaignId}/investigators";
}

/// <summary>Пределы полей листа — одни для сервиса и сообщений.</summary>
public static class CharacterLimits
{
    public const int NameLength = 200;
    public const int TextLength = 20000;
    public const int MaxListItems = 500;
}

/// <summary>
/// Лист целиком: документ (<see cref="Sheet"/>) и то, что о нём знает строка. Игрок — из владельца строки
/// (псевдоним в кампании или имя профиля), а не поле документа: в v1 «Сохранить» затирало имя игрока именем
/// слота или строкой «Unknown».
/// </summary>
public sealed class CharacterDto
{
    public Guid Id { get; set; }

    public CharacterKind Kind { get; set; }

    public CharacterStatus Status { get; set; }

    /// <summary>Версия строки (<c>xmin</c>) — её присылают в <c>If-Match</c> на запись.</summary>
    public uint Version { get; set; }

    public CharacterSheet Sheet { get; set; } = new();

    public Guid? PortraitFileId { get; set; }

    public string? PortraitUrl { get; set; }

    /// <summary>Игрок: псевдоним в кампании или имя профиля; почты не бывает. Null — у прегена и НПС.</summary>
    public string? PlayerName { get; set; }

    public Guid? CampaignId { get; set; }

    public string? CampaignName { get; set; }

    /// <summary>Эпоха кампании — по ней фаза развития пересчитывает деньги (таблица II); без кампании — классика.</summary>
    public Era Era { get; set; } = Era.Classic;

    public Guid? ScenarioId { get; set; }

    public string? ScenarioName { get; set; }

    public bool CanEdit { get; set; }

    public bool CanDelete { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}

/// <summary>Итог записи: новая версия для следующего <c>If-Match</c>.</summary>
public sealed record CharacterSavedDto(uint Version, DateTimeOffset UpdatedAt);

/// <summary>Портрет — строка <c>files</c> (загрузка — модуль Files); null — убрать.</summary>
public sealed record SetPortraitRequest(Guid? FileId);

public sealed record SetStatusRequest(CharacterStatus Status);

/// <summary>Сосед по столу: имя, профессия и игрок — из живого листа, без документа.</summary>
public sealed record PartyMemberDto(Guid CharacterId, string Name, string? Occupation, string? PlayerName);

/// <summary>Сыщик кампании для проверки Хранителя (групповая проверка ширмы): лист только для чтения.</summary>
public sealed record InvestigatorDto(Guid CharacterId, string Name, string? PlayerName, CharacterSheet Sheet);

/// <summary>
/// Лист сыщика. Ошибки — <see cref="Platform.ApiException"/>: 404 — листа нет или он не виден (неразличимо),
/// 403 — виден, но править нельзя, 409 <c>stale</c> — лист изменили на другом устройстве, 428 — без версии,
/// 400 — документ не прошёл проверку.
/// </summary>
public interface ICharactersApi
{
    Task<CharacterDto> GetAsync(Guid characterId, CancellationToken cancellationToken = default);

    Task<CharacterSavedDto> SaveSheetAsync(Guid characterId, CharacterSheet sheet, uint version, CancellationToken cancellationToken = default);

    Task<CharacterSavedDto> SetPortraitAsync(Guid characterId, Guid? fileId, uint version, CancellationToken cancellationToken = default);

    Task<CharacterSavedDto> SetStatusAsync(Guid characterId, CharacterStatus status, uint version, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PartyMemberDto>> GetPartyAsync(Guid characterId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<InvestigatorDto>> GetCampaignInvestigatorsAsync(Guid campaignId, CancellationToken cancellationToken = default);
}
