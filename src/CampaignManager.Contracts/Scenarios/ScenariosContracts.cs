using CampaignManager.Contracts.Characters;
using CampaignManager.Core;
using CampaignManager.Core.Campaigns;
using CampaignManager.Core.Catalogs;
using CampaignManager.Core.Scenarios;

namespace CampaignManager.Contracts.Scenarios;

/// <summary>
/// Сценарии — общая библиотека Хранителей (T2.5a): список, рабочее место одним запросом, а правка — по одной
/// строке на запрос. Каждая вложенная сущность (локация, проверка, факт, раздатка, тварь, предмет, НПС, преген) —
/// своя строка и свой адрес: правка одной вкладки не затирает другую (в v1 сценарий писался строкой целиком
/// с пятью JSONB, и правки терялись). Шапка и текст — корень с версией (<c>If-Match</c>).
/// </summary>
public static class ScenariosRoutes
{
    /// <summary><c>GET</c> — <see cref="ScenarioListDto"/>; <c>POST</c> <see cref="ScenarioInput"/> → 201 <see cref="ScenarioDto"/>.</summary>
    public const string Scenarios = ApiRoutes.Prefix + "/scenarios";

    /// <summary><c>GET</c> — рабочее место <see cref="ScenarioDto"/> с <c>ETag</c>; <c>PUT</c> <see cref="ScenarioInput"/> с <c>If-Match</c>; <c>DELETE</c>.</summary>
    public const string ScenarioPattern = Scenarios + "/{scenarioId:guid}";

    /// <summary><c>PUT</c> <see cref="ScenarioTextInput"/> с <c>If-Match</c> — основной текст Хранителя.</summary>
    public const string TextPattern = ScenarioPattern + "/text";

    /// <summary><c>PUT</c> <see cref="ReorderRequest"/> — порядок строк одной части.</summary>
    public const string OrderPattern = ScenarioPattern + "/order";

    /// <summary><c>GET</c> — прохождения сценария в кампаниях, которые ведёт вошедший (<see cref="ScenarioRunDto"/>): режим игры
    /// берёт из них сыщиков для проверок (T2.5b); <c>POST</c> <see cref="PlayInCampaignRequest"/> — «Играть в кампании» (T2.5c,
    /// <see cref="IRunsApi"/>).</summary>
    public const string RunsPattern = ScenarioPattern + "/runs";

    /// <summary><c>POST</c> <see cref="AnnounceOneShotRequest"/> — «Объявить ваншот»: кампания, Хранитель и прохождение разом.</summary>
    public const string OneShotPattern = ScenarioPattern + "/oneshot";

    /// <summary><c>POST</c> <see cref="LocationInput"/>.</summary>
    public const string LocationsPattern = ScenarioPattern + "/locations";

    /// <summary><c>PUT</c> <see cref="LocationInput"/>; <c>DELETE</c> — с вложенными локациями и проверками.</summary>
    public const string LocationPattern = LocationsPattern + "/{locationId:guid}";

    /// <summary><c>PUT</c> <see cref="LocationMusicInput"/> — музыка локации: настроения и прибитые треки (T2.5b).</summary>
    public const string LocationMusicPattern = LocationPattern + "/music";

    /// <summary><c>POST</c> <see cref="CheckInput"/> — проверка в локации.</summary>
    public const string LocationChecksPattern = LocationPattern + "/checks";

    /// <summary><c>PUT</c> <see cref="CheckInput"/>; <c>DELETE</c>.</summary>
    public const string CheckPattern = ScenarioPattern + "/checks/{checkId:guid}";

    /// <summary><c>POST</c> <see cref="KeyFactInput"/>.</summary>
    public const string FactsPattern = ScenarioPattern + "/facts";

    public const string FactPattern = FactsPattern + "/{factId:guid}";

    /// <summary><c>POST</c> <see cref="HandoutInput"/>.</summary>
    public const string HandoutsPattern = ScenarioPattern + "/handouts";

    public const string HandoutPattern = HandoutsPattern + "/{handoutId:guid}";

    /// <summary><c>GET</c> — раздатка для показа игрокам (<see cref="HandoutScreenDto"/>): без пометки и заметки Хранителя.
    /// Читает Хранитель и игрок кампании, где сценарий проходят (<c>ForHandoutAsync</c>).</summary>
    public const string HandoutScreenPattern = HandoutPattern + "/screen";

    /// <summary><c>POST</c> <see cref="ScenarioCreatureInput"/> — тварь из бестиария.</summary>
    public const string CreaturesPattern = ScenarioPattern + "/creatures";

    public const string CreaturePattern = CreaturesPattern + "/{creatureRowId:guid}";

    /// <summary><c>POST</c> <see cref="ScenarioItemInput"/> — предмет справочника или свой реквизит.</summary>
    public const string ItemsPattern = ScenarioPattern + "/items";

    public const string ItemPattern = ItemsPattern + "/{itemRowId:guid}";

    /// <summary><c>PUT</c> <see cref="NpcCastInput"/> — занять НПС или поменять роль; <c>DELETE</c> — убрать связь (лист остаётся).</summary>
    public const string NpcPattern = ScenarioPattern + "/npcs/{characterId:guid}";

    /// <summary><c>POST</c> <see cref="AddPregenRequest"/> — копия прегена из библиотеки.</summary>
    public const string PregensPattern = ScenarioPattern + "/pregens";

    /// <summary><c>DELETE</c> — убрать прегена из сценария (в архив библиотеки).</summary>
    public const string PregenPattern = PregensPattern + "/{characterId:guid}";

    public static string Scenario(Guid scenarioId) => $"{Scenarios}/{scenarioId}";

    public static string Text(Guid scenarioId) => $"{Scenario(scenarioId)}/text";

    public static string Order(Guid scenarioId) => $"{Scenario(scenarioId)}/order";

    public static string Runs(Guid scenarioId) => $"{Scenario(scenarioId)}/runs";

    public static string OneShot(Guid scenarioId) => $"{Scenario(scenarioId)}/oneshot";

    public static string Locations(Guid scenarioId) => $"{Scenario(scenarioId)}/locations";

    public static string Location(Guid scenarioId, Guid locationId) => $"{Locations(scenarioId)}/{locationId}";

    public static string LocationMusic(Guid scenarioId, Guid locationId) => $"{Location(scenarioId, locationId)}/music";

    public static string LocationChecks(Guid scenarioId, Guid locationId) => $"{Location(scenarioId, locationId)}/checks";

    public static string Check(Guid scenarioId, Guid checkId) => $"{Scenario(scenarioId)}/checks/{checkId}";

    public static string Facts(Guid scenarioId) => $"{Scenario(scenarioId)}/facts";

    public static string Fact(Guid scenarioId, Guid factId) => $"{Facts(scenarioId)}/{factId}";

    public static string Handouts(Guid scenarioId) => $"{Scenario(scenarioId)}/handouts";

    public static string Handout(Guid scenarioId, Guid handoutId) => $"{Handouts(scenarioId)}/{handoutId}";

    public static string HandoutScreen(Guid scenarioId, Guid handoutId) => $"{Handout(scenarioId, handoutId)}/screen";

    public static string Creatures(Guid scenarioId) => $"{Scenario(scenarioId)}/creatures";

    public static string Creature(Guid scenarioId, Guid rowId) => $"{Creatures(scenarioId)}/{rowId}";

    public static string Items(Guid scenarioId) => $"{Scenario(scenarioId)}/items";

    public static string Item(Guid scenarioId, Guid rowId) => $"{Items(scenarioId)}/{rowId}";

    public static string Npc(Guid scenarioId, Guid characterId) => $"{Scenario(scenarioId)}/npcs/{characterId}";

    public static string Pregens(Guid scenarioId) => $"{Scenario(scenarioId)}/pregens";

    public static string Pregen(Guid scenarioId, Guid characterId) => $"{Pregens(scenarioId)}/{characterId}";
}

/// <summary>Пределы полей — одни для формы, сервиса и сообщений.</summary>
public static class ScenarioLimits
{
    public const int NameLength = 200;
    public const int ShortTextLength = 300;
    public const int TextLength = 20000;

    /// <summary>Основной текст Хранителя: у перенесённого «Безымянного тумана» — 16,5 тыс. знаков.</summary>
    public const int BodyLength = 100000;

    public const int MaxCount = 99;
}

/// <summary>Список сценариев и можно ли завести новый (Хранитель по роли).</summary>
public sealed record ScenarioListDto(IReadOnlyList<ScenarioSummaryDto> Items, bool CanCreate);

/// <summary>Строка библиотеки сценариев: без содержимого, со счётчиками.</summary>
public sealed class ScenarioSummaryDto
{
    public Guid Id { get; set; }

    public string Name { get; set; } = "";

    public string? Summary { get; set; }

    public string? Setting { get; set; }

    public Era? Era { get; set; }

    public string? SettingDate { get; set; }

    /// <summary>Сценарий — переделка другого (форк): название исходного.</summary>
    public string? SourceScenarioName { get; set; }

    public int LocationCount { get; set; }

    public int HandoutCount { get; set; }

    public int NpcCount { get; set; }

    public int PregenCount { get; set; }

    /// <summary>Сколько прохождений в кампаниях: такой сценарий не удалить, пока они есть.</summary>
    public int RunCount { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public bool CanEdit { get; set; }

    public bool CanDelete { get; set; }
}

/// <summary>Шапка сценария — одна форма на создание и правку.</summary>
public sealed record ScenarioInput(string Name, string? Summary = null, string? Setting = null, Era? Era = null, string? SettingDate = null);

/// <summary>Основной текст Хранителя в Markdown.</summary>
public sealed record ScenarioTextInput(string? BodyMd);

/// <summary>Итог записи корня: новая версия для следующего <c>If-Match</c>.</summary>
public sealed record ScenarioSavedDto(uint Version, DateTimeOffset UpdatedAt);

/// <summary>
/// Рабочее место сценария одним запросом: шапка, текст и все вложенные части. Пишется не им, а по строке
/// (адреса <see cref="ScenariosRoutes"/>). <see cref="Version"/> — версия корня (шапка и текст).
/// </summary>
public sealed class ScenarioDto
{
    public Guid Id { get; set; }

    public uint Version { get; set; }

    public string Name { get; set; } = "";

    public string? Summary { get; set; }

    public string? BodyMd { get; set; }

    public string? Setting { get; set; }

    public Era? Era { get; set; }

    public string? SettingDate { get; set; }

    /// <summary>Автор — имя профиля (почт нет); null — автор неизвестен (старые записи v1).</summary>
    public string? AuthorName { get; set; }

    public Guid? SourceScenarioId { get; set; }

    public string? SourceScenarioName { get; set; }

    public int RunCount { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public bool CanEdit { get; set; }

    public bool CanDelete { get; set; }

    /// <summary>Все локации плоским списком по порядку; дерево — по <see cref="ScenarioLocationDto.ParentId"/>.</summary>
    public List<ScenarioLocationDto> Locations { get; set; } = [];

    public List<KeyFactDto> KeyFacts { get; set; } = [];

    public List<HandoutDto> Handouts { get; set; } = [];

    public List<ScenarioCreatureDto> Creatures { get; set; } = [];

    public List<ScenarioItemDto> Items { get; set; } = [];

    public List<ScenarioNpcDto> Npcs { get; set; } = [];

    public List<ScenarioPregenDto> Pregens { get; set; } = [];
}

public sealed class ScenarioLocationDto
{
    public Guid Id { get; set; }

    public Guid? ParentId { get; set; }

    public int Ord { get; set; }

    public string Name { get; set; } = "";

    public string? Address { get; set; }

    /// <summary>Описание для Хранителя, Markdown.</summary>
    public string? Description { get; set; }

    /// <summary>Настроение для фонотеки (правит T2.5b); правка локации их не трогает.</summary>
    public List<string> MusicTags { get; set; } = [];

    /// <summary>Прибитые треки (<c>location_tracks</c>, T2.5b).</summary>
    public List<Guid> TrackIds { get; set; } = [];

    public List<ScenarioCheckDto> Checks { get; set; } = [];
}

/// <summary>Локация: родитель — локация того же сценария; не она сама и не её потомок.</summary>
public sealed record LocationInput(string Name, string? Address = null, string? Description = null, Guid? ParentId = null);

/// <summary>Проверка в локации: навык (<see cref="SkillId"/>), характеристика или Удача.</summary>
public sealed class ScenarioCheckDto
{
    public Guid Id { get; set; }

    public Guid LocationId { get; set; }

    public int Ord { get; set; }

    public CheckTarget TargetKind { get; set; }

    public Guid? SkillId { get; set; }

    /// <summary>Название навыка из справочника — для показа.</summary>
    public string? SkillName { get; set; }

    public Characteristic? Characteristic { get; set; }

    public Difficulty Difficulty { get; set; }

    public string? OnSuccess { get; set; }

    public string? OnFailure { get; set; }
}

public sealed record CheckInput(
    CheckTarget TargetKind,
    Guid? SkillId = null,
    Characteristic? Characteristic = null,
    Difficulty Difficulty = Difficulty.Regular,
    string? OnSuccess = null,
    string? OnFailure = null);

public sealed record KeyFactDto(Guid Id, int Ord, KeyFactType Type, string Title, string? Content);

public sealed record KeyFactInput(KeyFactType Type, string Title, string? Content = null);

/// <summary>
/// Раздатка: <see cref="Name"/> — пометка Хранителя, игрокам не показывается; <see cref="PlayerText"/> — то, что
/// видят игроки; <see cref="KeeperNote"/> — только Хранителю (никогда не в показ и не на второй экран, T2.5b).
/// </summary>
public sealed record HandoutDto(Guid Id, int Ord, string Name, string? PlayerText, string? KeeperNote, Guid? FileId, string? FileUrl);

/// <summary>
/// Раздатка, как её видят игроки: картинка и текст. Пометки (<see cref="HandoutDto.Name"/>) и заметки Хранителя
/// (<see cref="HandoutDto.KeeperNote"/>) в типе нет вовсе — показ и второй экран не могут их вывести даже по ошибке.
/// <see cref="CanManage"/> — смотрит ведущий (Хранитель): «Закрыть» ведёт его к сценарию, игрока — на главную.
/// </summary>
public sealed record HandoutScreenDto(Guid Id, Guid ScenarioId, string? PlayerText, string? FileUrl, bool CanManage)
{
    /// <summary>То, что можно показать игрокам из раздатки рабочего места.</summary>
    public static HandoutScreenDto Of(Guid scenarioId, HandoutDto handout, bool canManage) =>
        new(handout.Id, scenarioId, handout.PlayerText, handout.FileUrl, canManage);
}

public sealed record HandoutInput(string Name, string? PlayerText = null, string? KeeperNote = null, Guid? FileId = null);

/// <summary>
/// Тварь сценария: ссылка на бестиарий плюс необязательная своя версия статблока. <see cref="Statblock"/> — итоговый
/// (своя версия поверх бестиария): бой (T2.6) читает именно его.
/// </summary>
public sealed class ScenarioCreatureDto
{
    public Guid Id { get; set; }

    public int Ord { get; set; }

    public Guid? CreatureId { get; set; }

    /// <summary>Имя в бестиарии; null — тварь только этого сценария.</summary>
    public string? CatalogName { get; set; }

    /// <summary>Своё имя в сценарии («Вожак культистов»), иначе — из бестиария.</summary>
    public string Name { get; set; } = "";

    /// <summary>Переопределённое имя; null — как в бестиарии.</summary>
    public string? OwnName { get; set; }

    public CreatureType? Type { get; set; }

    public int Count { get; set; } = 1;

    public string? LocationNote { get; set; }

    public string? Notes { get; set; }

    /// <summary>Статблок переделан для сценария: правка в бестиарии сюда не дойдёт.</summary>
    public bool HasOwnStatblock { get; set; }

    public Statblock Statblock { get; set; } = new();

    public string? ImageUrl { get; set; }
}

/// <summary>
/// Тварь в сценарии. Добавить — <see cref="CreatureId"/> обязателен (тварь из бестиария); правка его не меняет.
/// <see cref="ResetStatblock"/> — выбросить свою версию статблока и снова идти за бестиарием.
/// </summary>
public sealed record ScenarioCreatureInput(
    Guid? CreatureId,
    string? Name = null,
    int Count = 1,
    string? LocationNote = null,
    string? Notes = null,
    bool ResetStatblock = false);

/// <summary>Предмет сценария: из справочника (<see cref="ItemId"/>) или свой реквизит (только имя и описание).</summary>
public sealed class ScenarioItemDto
{
    public Guid Id { get; set; }

    public int Ord { get; set; }

    public Guid? ItemId { get; set; }

    public string? CatalogName { get; set; }

    public string Name { get; set; } = "";

    public string? OwnName { get; set; }

    public string? Type { get; set; }

    /// <summary>Своё описание, иначе — из справочника.</summary>
    public string? Description { get; set; }

    public string? OwnDescription { get; set; }

    public string? LocationNote { get; set; }

    public string? Notes { get; set; }

    public string? ImageUrl { get; set; }
}

/// <summary>Без <see cref="ItemId"/> — свой реквизит, тогда имя обязательно. Правка <see cref="ItemId"/> не меняет.</summary>
public sealed record ScenarioItemInput(
    Guid? ItemId,
    string? Name = null,
    string? Description = null,
    string? LocationNote = null,
    string? Notes = null);

/// <summary>НПС в составе: связь с листом, роль и количество — у появления, не у листа.</summary>
public sealed record ScenarioNpcDto(CharacterSummaryDto Character, NpcRole Role, int Count, string? Notes);

/// <summary>Занять НПС в сценарии или поменять роль, количество (1–99) и заметку.</summary>
public sealed record NpcCastInput(NpcRole Role = NpcRole.Neutral, int Count = 1, string? Notes = null);

/// <summary>Преген сценария; <see cref="IsReserved"/> — забронирован в незавершённом прохождении, убрать нельзя.</summary>
public sealed record ScenarioPregenDto(CharacterSummaryDto Character, bool IsReserved);

/// <summary>Прегена из библиотеки — копией в сценарий: заготовка остаётся для других сценариев.</summary>
public sealed record AddPregenRequest(Guid PregenId);

/// <summary>
/// Музыка локации: настроения (нормализуются, как теги фонотеки) и прибитые треки — играют, даже если тегов у них нет.
/// Запись заменяет оба списка целиком.
/// </summary>
public sealed record LocationMusicInput(IReadOnlyList<string> Tags, IReadOnlyList<Guid> TrackIds);

/// <summary>
/// Прохождение сценария в кампании, которую ведёт вошедший: из её сыщиков режим игры предлагает выбор в проверке, а рабочее
/// место показывает анонс, запись и брони (T2.5c, <see cref="IRunsApi"/>). Время — UTC, показывается в поясе браузера.
/// </summary>
/// <param name="SignupOpen">Открыта запись: игроки бронируют прегенов сценария (ваншот).</param>
/// <param name="Reservations">Брони прегенов этого прохождения.</param>
public sealed record ScenarioRunDto(
    Guid Id,
    Guid CampaignId,
    string CampaignName,
    CampaignKind CampaignKind,
    ScenarioRunStatus Status,
    DateTimeOffset? ScheduledAt,
    string? Announcement,
    bool SignupOpen,
    IReadOnlyList<RunReservationDto> Reservations,
    bool CanEdit,
    bool CanDelete);

/// <summary>Бронь прегена в прохождении.</summary>
/// <param name="PlayerName">Кто забронировал — по псевдониму в кампании или имени; <c>null</c> — имени нет (почт нет).</param>
/// <param name="CharacterId">Копия листа у игрока; <c>null</c> — игрок её удалил.</param>
/// <param name="CanRelease">Снять бронь можно мне: сам игрок или Хранитель этой кампании.</param>
public sealed record RunReservationDto(Guid PregenId, string PregenName, string? PlayerName, Guid? CharacterId, bool CanRelease);

/// <summary>Часть сценария, у строк которой есть порядок.</summary>
public enum ScenarioPart
{
    Locations,
    Checks,
    KeyFacts,
    Handouts,
    Creatures,
    Items,
}

/// <summary>
/// Новый порядок строк одной части: строки получают места по очереди. Локации — соседи (один родитель),
/// проверки — одной локации. Строки, которых нет в списке, не трогаются.
/// </summary>
public sealed record ReorderRequest(ScenarioPart Part, IReadOnlyList<Guid> Ids);

/// <summary>
/// Сценарии. Отказы — <see cref="Platform.ApiException"/>: 404 — сценария нет или он не виден (игроку — всегда),
/// 403 — виден, но нельзя (удалить чужой), 400 — форма, 409 <c>stale</c> — шапку или текст изменили на другом
/// устройстве, 409 <c>in-use</c> — сценарий проходят, 428 — запись корня без версии.
/// </summary>
public interface IScenariosApi
{
    Task<ScenarioListDto> ListAsync(CancellationToken cancellationToken = default);

    Task<ScenarioDto> CreateAsync(ScenarioInput input, CancellationToken cancellationToken = default);

    Task<ScenarioDto> GetAsync(Guid scenarioId, CancellationToken cancellationToken = default);

    Task<ScenarioSavedDto> UpdateAsync(Guid scenarioId, ScenarioInput input, uint version, CancellationToken cancellationToken = default);

    Task<ScenarioSavedDto> SaveTextAsync(Guid scenarioId, string? bodyMd, uint version, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid scenarioId, CancellationToken cancellationToken = default);

    Task ReorderAsync(Guid scenarioId, ReorderRequest request, CancellationToken cancellationToken = default);

    /// <summary>Прохождения сценария в кампаниях, которые ведёт вошедший (администратору — все); незавершённые — первыми.</summary>
    Task<IReadOnlyList<ScenarioRunDto>> ListRunsAsync(Guid scenarioId, CancellationToken cancellationToken = default);

    /// <summary>Музыка локации: настроения и прибитые треки целиком. Трек не из фонотеки — 400.</summary>
    Task<ScenarioLocationDto> SetLocationMusicAsync(Guid scenarioId, Guid locationId, LocationMusicInput input, CancellationToken cancellationToken = default);

    /// <summary>Раздатка для показа игрокам — без пометки и заметки Хранителя. Не видна — 404.</summary>
    Task<HandoutScreenDto> GetHandoutScreenAsync(Guid scenarioId, Guid handoutId, CancellationToken cancellationToken = default);

    Task<ScenarioLocationDto> AddLocationAsync(Guid scenarioId, LocationInput input, CancellationToken cancellationToken = default);

    Task<ScenarioLocationDto> UpdateLocationAsync(Guid scenarioId, Guid locationId, LocationInput input, CancellationToken cancellationToken = default);

    Task DeleteLocationAsync(Guid scenarioId, Guid locationId, CancellationToken cancellationToken = default);

    Task<ScenarioCheckDto> AddCheckAsync(Guid scenarioId, Guid locationId, CheckInput input, CancellationToken cancellationToken = default);

    Task<ScenarioCheckDto> UpdateCheckAsync(Guid scenarioId, Guid checkId, CheckInput input, CancellationToken cancellationToken = default);

    Task DeleteCheckAsync(Guid scenarioId, Guid checkId, CancellationToken cancellationToken = default);

    Task<KeyFactDto> AddFactAsync(Guid scenarioId, KeyFactInput input, CancellationToken cancellationToken = default);

    Task<KeyFactDto> UpdateFactAsync(Guid scenarioId, Guid factId, KeyFactInput input, CancellationToken cancellationToken = default);

    Task DeleteFactAsync(Guid scenarioId, Guid factId, CancellationToken cancellationToken = default);

    Task<HandoutDto> AddHandoutAsync(Guid scenarioId, HandoutInput input, CancellationToken cancellationToken = default);

    Task<HandoutDto> UpdateHandoutAsync(Guid scenarioId, Guid handoutId, HandoutInput input, CancellationToken cancellationToken = default);

    Task DeleteHandoutAsync(Guid scenarioId, Guid handoutId, CancellationToken cancellationToken = default);

    Task<ScenarioCreatureDto> AddCreatureAsync(Guid scenarioId, ScenarioCreatureInput input, CancellationToken cancellationToken = default);

    Task<ScenarioCreatureDto> UpdateCreatureAsync(Guid scenarioId, Guid rowId, ScenarioCreatureInput input, CancellationToken cancellationToken = default);

    Task DeleteCreatureAsync(Guid scenarioId, Guid rowId, CancellationToken cancellationToken = default);

    Task<ScenarioItemDto> AddItemAsync(Guid scenarioId, ScenarioItemInput input, CancellationToken cancellationToken = default);

    Task<ScenarioItemDto> UpdateItemAsync(Guid scenarioId, Guid rowId, ScenarioItemInput input, CancellationToken cancellationToken = default);

    Task DeleteItemAsync(Guid scenarioId, Guid rowId, CancellationToken cancellationToken = default);

    /// <summary>Занять НПС (вид <c>Npc</c>, который вы видите) или поменять роль: повтор — правка, а не двойник.</summary>
    Task CastNpcAsync(Guid scenarioId, Guid characterId, NpcCastInput input, CancellationToken cancellationToken = default);

    /// <summary>Убрать НПС из состава: лист остаётся в библиотеке.</summary>
    Task RemoveNpcAsync(Guid scenarioId, Guid characterId, CancellationToken cancellationToken = default);

    Task<CharacterCreatedDto> AddPregenAsync(Guid scenarioId, Guid pregenId, CancellationToken cancellationToken = default);

    /// <summary>Убрать прегена из сценария — он уходит в архив библиотеки. Забронированного — 409.</summary>
    Task RemovePregenAsync(Guid scenarioId, Guid characterId, CancellationToken cancellationToken = default);
}
