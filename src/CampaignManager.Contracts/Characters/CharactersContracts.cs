using CampaignManager.Core;
using CampaignManager.Core.Characters;
using CampaignManager.Core.Scenarios;

namespace CampaignManager.Contracts.Characters;

/// <summary>
/// Листы: создание (T2.4), библиотека НПС и прегенов, чтение, запись документа с <c>If-Match</c>, портрет, статус,
/// соседи по столу.
/// </summary>
public static class CharactersRoutes
{
    /// <summary>
    /// <c>POST</c> <see cref="CreateCharacterRequest"/> → 201 <see cref="CharacterCreatedDto"/>;
    /// <c>GET ?kind=Npc|Pregen&amp;archived=</c> — библиотека: <see cref="CharacterSummaryDto"/>.
    /// </summary>
    public const string Characters = ApiRoutes.Prefix + "/characters";

    /// <summary><c>GET ?kind=&amp;campaignId=&amp;scenarioId=</c> — куда ляжет новый лист: <see cref="CreationContextDto"/>.</summary>
    public const string NewPattern = Characters + "/new";

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

    public static string Library(CharacterKind kind, bool archived) =>
        $"{Characters}?kind={kind}&archived={(archived ? "true" : "false")}";

    public static string New(CharacterKind kind, Guid? campaignId, Guid? scenarioId) =>
        $"{NewPattern}?kind={kind}"
        + (campaignId is { } campaign ? $"&campaignId={campaign}" : "")
        + (scenarioId is { } scenario ? $"&scenarioId={scenario}" : "");

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
    public const int SummaryBackstoryLength = 280;
    public const int MaxCastCount = 99;
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

    /// <summary>
    /// Лист вошедшего (он владелец строки): нового сыщика вместо выбывшего заводит игрок, а не Хранитель, хотя править лист
    /// могут оба.
    /// </summary>
    public bool IsMine { get; set; }

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
/// Новый лист (T2.4). Документ собирает клиент правилом Core (<c>SheetBuilder</c>: помощник, случайный сыщик, быстрый
/// НПС, чистый лист); сервер проверяет его так же, как запись листа. Кто владелец — решает вид:
/// <list type="bullet">
/// <item><c>Player</c> — вошедший; <see cref="CampaignId"/> — кампания, где он участник (null — пока без кампании);</item>
/// <item><c>Pregen</c> — <see cref="ScenarioId"/> сценария или null (библиотека);</item>
/// <item><c>Npc</c> — <see cref="CampaignId"/> кампании, которую ведёт Хранитель, или null (библиотека); <see cref="Cast"/> —
/// сразу занять в сценарии (быстрый НПС из сценария, T2.5a): лист и связь пишутся одной транзакцией, поэтому «лист
/// есть, а связи нет» (в v1 — двойник при повторе) не бывает.</item>
/// </list>
/// </summary>
public sealed class CreateCharacterRequest
{
    public CharacterKind Kind { get; set; }

    public Guid? CampaignId { get; set; }

    public Guid? ScenarioId { get; set; }

    public CharacterSheet Sheet { get; set; } = new();

    public NpcCastRequest? Cast { get; set; }
}

/// <summary>НПС в составе сценария: роль и количество — у появления, а не у листа (один НПС бывает и врагом, и союзником).</summary>
public sealed record NpcCastRequest(Guid ScenarioId, NpcRole Role = NpcRole.Neutral, int Count = 1, string? Notes = null);

public sealed record CharacterCreatedDto(Guid Id, uint Version);

/// <summary>
/// Куда ляжет новый лист — до первого шага помощника (знание v1: иначе игрок проходил все шаги и получал отказ на
/// «Создать»). Эпоха — кампании или сценария (имена, столбец таблицы II), без них — классика.
/// </summary>
public sealed class CreationContextDto
{
    public CharacterKind Kind { get; set; }

    public bool CanCreate { get; set; }

    /// <summary>Почему нельзя — для человека; null, если можно.</summary>
    public string? Reason { get; set; }

    public Guid? CampaignId { get; set; }

    public string? CampaignName { get; set; }

    public Guid? ScenarioId { get; set; }

    public string? ScenarioName { get; set; }

    public Era Era { get; set; } = Era.Classic;

    /// <summary>Активный сыщик этого игрока в кампании — второй активный не заводится (один активный лист).</summary>
    public Guid? ActiveCharacterId { get; set; }
}

/// <summary>
/// Строка библиотеки НПС и прегенов — то, что показывает <c>CharacterSummaryCard</c> (и она же в сценарии, T2.5a):
/// имя, занятие, возраст, где лежит, в каких сценариях занят, начало предыстории. Документа целиком нет.
/// </summary>
public sealed class CharacterSummaryDto
{
    public Guid Id { get; set; }

    public CharacterKind Kind { get; set; }

    public CharacterStatus Status { get; set; }

    /// <summary>Версия строки — для <c>If-Match</c> архивации из библиотеки.</summary>
    public uint Version { get; set; }

    public string Name { get; set; } = "";

    public string? Occupation { get; set; }

    public int Age { get; set; }

    public string? Gender { get; set; }

    public string? Residence { get; set; }

    /// <summary>Начало предыстории (до <see cref="CharacterLimits.SummaryBackstoryLength"/> знаков).</summary>
    public string? Backstory { get; set; }

    public string? PortraitUrl { get; set; }

    public Guid? CampaignId { get; set; }

    public string? CampaignName { get; set; }

    /// <summary>Сценарий прегена.</summary>
    public Guid? ScenarioId { get; set; }

    public string? ScenarioName { get; set; }

    /// <summary>Сценарии, где НПС занят (связи <c>scenario_npcs</c>).</summary>
    public List<string> CastIn { get; set; } = [];

    /// <summary>Те же связи с ролью и количеством — для чипа «Дом с привидением · враг ×3» (ревью g3, 70).</summary>
    public List<CharacterCastDto> Casts { get; set; } = [];

    /// <summary>ПЗ сейчас — строка карточки для стола: «ПЗ 11 · Ближний бой 40%» (ревью g3, 69).</summary>
    public int HitPoints { get; set; }

    /// <summary>Лучший боевой навык листа (ближний бой, стрельба, метание — без Уклонения); null — боевых строк нет.</summary>
    public string? CombatSkill { get; set; }

    public int CombatValue { get; set; }

    public bool CanEdit { get; set; }
}

/// <summary>Где занят НПС: сценарий, роль и количество (один лист — «3 бандита»).</summary>
public sealed record CharacterCastDto(string Scenario, NpcRole Role, int Count);

/// <summary>
/// Лист сыщика. Ошибки — <see cref="Platform.ApiException"/>: 404 — листа нет или он не виден (неразличимо),
/// 403 — виден, но править нельзя, 409 <c>stale</c> — лист изменили на другом устройстве, 428 — без версии,
/// 400 — документ не прошёл проверку.
/// </summary>
public interface ICharactersApi
{
    /// <summary>Новый лист: 403 — вид или место недоступны, 409 — у игрока уже есть активный сыщик в кампании, 400 — документ.</summary>
    Task<CharacterCreatedDto> CreateAsync(CreateCharacterRequest request, CancellationToken cancellationToken = default);

    Task<CreationContextDto> GetCreationContextAsync(CharacterKind kind, Guid? campaignId, Guid? scenarioId,
        CancellationToken cancellationToken = default);

    /// <summary>Библиотека НПС или прегенов (Хранитель): без архива или только архив.</summary>
    Task<IReadOnlyList<CharacterSummaryDto>> ListAsync(CharacterKind kind, bool archived, CancellationToken cancellationToken = default);

    Task<CharacterDto> GetAsync(Guid characterId, CancellationToken cancellationToken = default);

    Task<CharacterSavedDto> SaveSheetAsync(Guid characterId, CharacterSheet sheet, uint version, CancellationToken cancellationToken = default);

    Task<CharacterSavedDto> SetPortraitAsync(Guid characterId, Guid? fileId, uint version, CancellationToken cancellationToken = default);

    Task<CharacterSavedDto> SetStatusAsync(Guid characterId, CharacterStatus status, uint version, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PartyMemberDto>> GetPartyAsync(Guid characterId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<InvestigatorDto>> GetCampaignInvestigatorsAsync(Guid campaignId, CancellationToken cancellationToken = default);
}
