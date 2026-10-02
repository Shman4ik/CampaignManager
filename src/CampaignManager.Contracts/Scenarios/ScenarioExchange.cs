using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using CampaignManager.Core;
using CampaignManager.Core.Catalogs;
using CampaignManager.Core.Characters;
using CampaignManager.Core.Scenarios;

namespace CampaignManager.Contracts.Scenarios;

/// <summary>
/// Обмен сценарием одним JSON-файлом (T2.5d): экспорт отдаёт ровно то, что принимает импорт. Формат — v1 и надстройка над
/// ним: <b>без идентификаторов</b> (автор файла — человек или LLM, переносящая сценарий из книги, — их не знает), родитель
/// локации — по имени, навык проверки — по имени (или «ИНТ», «Удача»), НПС — по имени из библиотеки, тварь, предмет и трек —
/// по имени справочника. Файл v1 читается как есть; поля анонса v1 (<c>isTemplate</c>, <c>scheduledDate</c>…) больше
/// никуда не идут — анонс у прохождения (T2.5c).
/// </summary>
public static class ScenarioExchangeRoutes
{
    /// <summary><c>POST</c> — тело: файл сценария как есть; <c>?dryRun=true</c> — пробный прогон, <c>?name=</c> — название копии.</summary>
    public const string Import = ScenariosRoutes.Scenarios + "/import";

    /// <summary><c>GET</c> — файл сценария (<c>Content-Disposition: attachment</c>).</summary>
    public const string ExportPattern = ScenariosRoutes.ScenarioPattern + "/export";

    public const string DryRunQuery = "dryRun";

    public const string NameQuery = "name";

    public static string Export(Guid scenarioId) => $"{ScenariosRoutes.Scenario(scenarioId)}/export";
}

/// <summary>Файл сценария. Имена полей v1 сохранены: <c>description</c> — кратко, <c>location</c> — место, <c>era</c> — время действия текстом, <c>journal</c> — основной текст.</summary>
public sealed class ScenarioFile
{
    public string Name { get; set; } = "";

    /// <summary>Кратко (v1 <c>Description</c>).</summary>
    public string? Description { get; set; }

    /// <summary>Место действия (v1 <c>Location</c>).</summary>
    public string? Location { get; set; }

    /// <summary>Время действия текстом: «Июнь 1925 года» (v1 <c>Era</c>).</summary>
    public string? Era { get; set; }

    /// <summary>Эпоха (2.0): <c>Classic</c> | <c>Modern</c>. Нет — угадывается по году в <see cref="Era"/>.</summary>
    public Era? Epoch { get; set; }

    /// <summary>Основной текст Хранителя, Markdown (v1 <c>Journal</c>).</summary>
    public string? Journal { get; set; }

    public List<ScenarioFileFact> KeyFacts { get; set; } = [];

    /// <summary>Локации по порядку; вложенная — после родителя (так их пишет экспорт: дерево в глубину).</summary>
    public List<ScenarioFileLocation> Locations { get; set; } = [];

    public List<ScenarioFileHandout> Handouts { get; set; } = [];

    /// <summary>Твари из бестиария (2.0; v1 их не переносил).</summary>
    public List<ScenarioFileCreature> Creatures { get; set; } = [];

    /// <summary>Предметы справочника и свой реквизит (2.0).</summary>
    public List<ScenarioFileItem> Items { get; set; } = [];

    /// <summary>Состав НПС. Лист с таким же именем в библиотеке — занимается он, а не заводится двойник.</summary>
    public List<ScenarioFileNpc> Npcs { get; set; } = [];

    /// <summary>Прегены сценария (2.0): всегда новые листы — преген принадлежит сценарию.</summary>
    public List<ScenarioFileCharacter> Pregens { get; set; } = [];

    // ── Поля анонса v1: читаются, чтобы файл v1 не падал, и отбрасываются с предупреждением ──

    public bool? IsTemplate { get; set; }

    public bool? IsPublished { get; set; }

    public string? ScheduledDate { get; set; }

    public string? AnnouncementText { get; set; }
}

public sealed class ScenarioFileFact
{
    public string Title { get; set; } = "";

    public KeyFactType Type { get; set; } = KeyFactType.Backstory;

    public string? Content { get; set; }
}

public sealed class ScenarioFileLocation
{
    public string Name { get; set; } = "";

    public string? Address { get; set; }

    public string? Description { get; set; }

    /// <summary>Имя родительской локации из этого же файла; не нашлась — локация на верхнем уровне и предупреждение.</summary>
    public string? Parent { get; set; }

    public List<ScenarioFileCheck> SkillChecks { get; set; } = [];

    /// <summary>Настроение для фонотеки (2.0).</summary>
    public List<string>? MusicTags { get; set; }

    /// <summary>Прибитые треки фонотеки по названию (2.0).</summary>
    public List<string>? Tracks { get; set; }
}

public sealed class ScenarioFileCheck
{
    /// <summary>Навык по имени справочника (без регистра и «ё», старые написания v1), характеристика («ИНТ») или «Удача».</summary>
    public string SkillName { get; set; } = "";

    /// <summary>Пусто — обычная; <c>Hard</c> — трудная; <c>Extreme</c> — чрезвычайная.</summary>
    public string? Difficulty { get; set; }

    public string? SuccessResult { get; set; }

    public string? FailureResult { get; set; }
}

public sealed class ScenarioFileHandout
{
    /// <summary>Пометка Хранителя: игрокам не показывается.</summary>
    public string Name { get; set; } = "";

    /// <summary>Текст для игроков (v1 <c>Description</c>).</summary>
    public string? Description { get; set; }

    /// <summary>Картинка — адрес файла этой базы (<c>/api/v1/files/{id}</c>); из другой базы — пропускается с предупреждением.</summary>
    public string? FileUrl { get; set; }

    /// <summary>Только Хранителю.</summary>
    public string? KeeperNote { get; set; }
}

public sealed class ScenarioFileCreature
{
    /// <summary>Тварь бестиария по названию; нет — тварь только этого сценария, тогда нужны <see cref="Name"/> и <see cref="Statblock"/>.</summary>
    public string? Creature { get; set; }

    /// <summary>Своё имя в сценарии («Вожак культистов»).</summary>
    public string? Name { get; set; }

    public int Count { get; set; } = 1;

    /// <summary>Где: «в подвале особняка».</summary>
    public string? Location { get; set; }

    public string? Notes { get; set; }

    /// <summary>Своя версия статблока (переделка для сценария); нет — как в бестиарии. Навыки — по имени.</summary>
    public Statblock? Statblock { get; set; }
}

public sealed class ScenarioFileItem
{
    /// <summary>Предмет справочника по названию; нет — свой реквизит, тогда нужно <see cref="Name"/>.</summary>
    public string? Item { get; set; }

    public string? Name { get; set; }

    public string? Description { get; set; }

    public string? Location { get; set; }

    public string? Notes { get; set; }
}

/// <summary>
/// Персонаж как напечатан в книге (формат v1): характеристики, ПЗ, ПМ, БкУ, Комплекция, Скорость, Уклонение и навыки
/// «имя → значение». Сверх v1 — графы биографии, оружие, заклинания, снаряжение, деньги и портрет: их отдаёт экспорт
/// прегенов. Лист собирает <c>SheetBuilder.FromImport</c> — так же, как у помощника.
/// </summary>
public class ScenarioFileCharacter
{
    public string Name { get; set; } = "";

    public string? Occupation { get; set; }

    public int Age { get; set; }

    public string? Gender { get; set; }

    public string? Backstory { get; set; }

    public Characteristics Characteristics { get; set; } = new();

    public int HitPoints { get; set; }

    public int MagicPoints { get; set; }

    public int Sanity { get; set; }

    public int Luck { get; set; }

    public string? DamageBonus { get; set; }

    public string? Build { get; set; }

    public int MoveSpeed { get; set; }

    public int Dodge { get; set; }

    public Dictionary<string, int> Skills { get; set; } = [];

    public string? Birthplace { get; set; }

    public string? Residence { get; set; }

    /// <summary>Графы биографии (2.0); предыстория — в <see cref="Backstory"/>.</summary>
    public Biography? Biography { get; set; }

    public List<ScenarioFileWeapon>? Weapons { get; set; }

    public List<ScenarioFileSpell>? Spells { get; set; }

    public List<EquipmentItem>? Equipment { get; set; }

    public Finances? Finances { get; set; }

    /// <summary>Портрет — адрес файла этой базы, как у раздатки.</summary>
    public string? PortraitUrl { get; set; }
}

/// <summary>НПС: лист и его появление в этом сценарии (роль, сколько, заметка).</summary>
public sealed class ScenarioFileNpc : ScenarioFileCharacter
{
    public NpcRole Role { get; set; } = NpcRole.Neutral;

    public int Count { get; set; } = 1;

    public string? Notes { get; set; }
}

/// <summary>Оружие на листе: текст книги; навык и запись справочника — по имени.</summary>
public sealed class ScenarioFileWeapon
{
    public string Name { get; set; } = "";

    public string? Skill { get; set; }

    public string? Damage { get; set; }

    public string? Range { get; set; }

    public string? Attacks { get; set; }

    public string? Ammo { get; set; }

    public string? Malfunction { get; set; }

    public bool Impaling { get; set; }

    public string? Notes { get; set; }
}

/// <summary>Заклинание на листе — свой экземпляр; запись справочника — по имени.</summary>
public sealed class ScenarioFileSpell
{
    public string Name { get; set; } = "";

    public List<string>? AlternativeNames { get; set; }

    public string? Cost { get; set; }

    public string? CastingTime { get; set; }

    public string? Description { get; set; }
}

public enum ScenarioImportPart
{
    Scenario,
    Location,
    Check,
    KeyFact,
    Handout,
    Creature,
    Item,
    Npc,
    Pregen,
}

public enum ScenarioImportOutcome
{
    /// <summary>Заведена (или заведётся — в пробном прогоне).</summary>
    Created,

    /// <summary>НПС из библиотеки: занят существующий лист.</summary>
    Reused,

    /// <summary>Не записана; из-за неё не записывается весь файл.</summary>
    Failed,
}

/// <param name="Message">Почему не записано или что сделано не так, как в файле (предупреждение).</param>
public sealed record ScenarioImportLine(ScenarioImportPart Part, string Name, ScenarioImportOutcome Outcome, string? Message);

/// <summary>
/// Итог импорта. Импорт — одна транзакция: есть хоть одна строка <see cref="ScenarioImportOutcome.Failed"/> — не записано
/// ничего (<see cref="Imported"/> = false), как и в пробном прогоне (<see cref="DryRun"/>). Счётчики — что заведено бы или заведено.
/// </summary>
public sealed class ScenarioImportReport
{
    public bool DryRun { get; set; }

    public bool Imported { get; set; }

    /// <summary>Новый сценарий — только если записан.</summary>
    public Guid? ScenarioId { get; set; }

    public string ScenarioName { get; set; } = "";

    public int Locations { get; set; }

    public int Checks { get; set; }

    public int KeyFacts { get; set; }

    public int Handouts { get; set; }

    public int Creatures { get; set; }

    public int Items { get; set; }

    public int NpcsCreated { get; set; }

    public int NpcsReused { get; set; }

    public int Pregens { get; set; }

    public int Failed { get; set; }

    /// <summary>Строки с итогом: каждая часть файла; предупреждение — в <see cref="ScenarioImportLine.Message"/>.</summary>
    public List<ScenarioImportLine> Lines { get; set; } = [];

    /// <summary>Общие предупреждения (поля анонса v1 и т. п.).</summary>
    public List<string> Warnings { get; set; } = [];
}

/// <summary>Импорт и экспорт сценария. Отказы — <see cref="Platform.ApiException"/>: 400 — файл не читается, 403/404 — права.</summary>
public interface IScenarioExchangeApi
{
    /// <param name="name">Название копии вместо названия из файла; пусто — из файла.</param>
    Task<ScenarioImportReport> ImportAsync(Stream file, bool dryRun, string? name = null, CancellationToken cancellationToken = default);

    /// <summary>Файл сценария — тем же форматом, что принимает импорт.</summary>
    Task<string> ExportAsync(Guid scenarioId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Файл сценария читается и пишется этим контекстом, а не <see cref="ContractsJsonContext"/>: пустые поля не пишутся, отступы
/// и кириллица как есть — файл читают и правят люди и LLM. Читает без учёта регистра имён, с комментариями и висячими запятыми.
/// </summary>
[JsonSourceGenerationOptions(JsonSerializerDefaults.Web, UseStringEnumConverter = true, WriteIndented = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull, ReadCommentHandling = JsonCommentHandling.Skip,
    AllowTrailingCommas = true)]
[JsonSerializable(typeof(ScenarioFile))]
public sealed partial class ScenarioFileJsonContext : JsonSerializerContext
{
    private static ScenarioFileJsonContext? _readable;

    /// <summary>Контекст с кириллицей без <c>\u</c>-экранирования: файл открывают в редакторе.</summary>
    public static ScenarioFileJsonContext Readable =>
        _readable ??= new ScenarioFileJsonContext(new JsonSerializerOptions(Default.Options)
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        });
}
