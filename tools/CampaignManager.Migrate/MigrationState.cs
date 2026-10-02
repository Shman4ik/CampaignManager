using CampaignManager.Core.Characters;
using CampaignManager.Data;
using CampaignManager.Data.Catalogs;
using CampaignManager.Data.Files;
using CampaignManager.Migrate.Catalogs;
using CampaignManager.Migrate.Files;
using CampaignManager.Migrate.V1;

namespace CampaignManager.Migrate;

/// <summary>Настройки прогона.</summary>
public sealed record MigrationOptions
{
    /// <summary>
    /// Часовой пояс Хранителя: <c>Scenarios.ScheduledDate</c> v1 — время «как ввели» без пояса
    /// (<c>timestamp without time zone</c>). По умолчанию — Москва (вопрос владельцу в PR T1.3).
    /// </summary>
    public string KeeperTimeZone { get; init; } = "Europe/Moscow";
}

/// <summary>
/// Общее состояние шагов переноса: контекст <c>cm</c>, отчёт и соответствия «v1 → новая строка». Id строк
/// v1 сохраняются везде, где запись переезжает один к одному (ссылки и закладки не ломаются).
/// </summary>
public sealed class MigrationState(V1Database v1, CmDbContext db, IFileStore files, MigrationReport report, MigrationOptions options)
{
    public V1Database V1 { get; } = v1;

    public CmDbContext Db { get; } = db;

    public MigrationReport Report { get; } = report;

    public MigrationOptions Options { get; } = options;

    /// <summary>Почта v1 (в нижнем регистре) → пользователь <c>cm.users</c>.</summary>
    public Dictionary<string, Guid> UserByEmail { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Перенесённые навыки по id v1 (он же id в <c>cm</c>).</summary>
    public Dictionary<Guid, Skill> Skills { get; } = [];

    /// <summary>Справочник навыков для правил Core — собирается после шага справочников.</summary>
    public SkillCatalog SkillCatalog { get; set; } = new([]);

    /// <summary>Навык v1 по имени — по справочнику, перенесённому выше.</summary>
    public SkillResolver Resolver { get; set; } = new(new SkillCatalog([]));

    /// <summary>Реквизит сценария из справочника v1 (имя → строка v1): переносится предметом сценария.</summary>
    public Dictionary<string, System.Text.Json.Nodes.JsonObject> ScenarioProps { get; } = new(StringComparer.Ordinal);

    public Dictionary<Guid, Weapon> Weapons { get; } = [];

    public Dictionary<Guid, Spell> Spells { get; } = [];

    public Dictionary<Guid, Occupation> Occupations { get; } = [];

    /// <summary>Предметы справочника по имени v1.</summary>
    public Dictionary<string, Item> ItemsByV1Name { get; } = new(StringComparer.Ordinal);

    /// <summary>Тварь справочника по имени v1 и её статблок в JSON Core (для сравнения с тварью сценария).</summary>
    public Dictionary<string, (Creature Creature, string StatblockJson)> CreaturesByV1Name { get; } = new(StringComparer.Ordinal);

    public HashSet<Guid> Tracks { get; } = [];

    public HashSet<Guid> Scenarios { get; } = [];

    /// <summary>Место игрока v1 (<c>CampaignPlayers.Id</c>) → кампания и пользователь.</summary>
    public Dictionary<Guid, (Guid CampaignId, Guid UserId)> CampaignPlayers { get; } = [];

    public Dictionary<Guid, Data.Campaigns.Campaign> Campaigns { get; } = [];

    public HashSet<Guid> Characters { get; } = [];

    private readonly Dictionary<string, StoredFile> _filesByKey = new(StringComparer.Ordinal);

    public int MissingFiles { get; private set; }

    public Guid? UserId(string? email) =>
        email is { Length: > 0 } && UserByEmail.TryGetValue(email.Trim(), out var id) ? id : null;

    /// <summary>
    /// Строка <c>files</c> для ключа объекта v1: объект копируется в бакет среды, размер и тип — из
    /// хранилища. Не нашёлся в источнике — в отчёт и null (ссылка не переносится, SCHEMA).
    /// </summary>
    public async Task<StoredFile?> StoredFileAsync(string key, string where, CancellationToken cancellationToken)
    {
        key = key.Trim().TrimStart('/');
        if (_filesByKey.TryGetValue(key, out var known))
        {
            return known;
        }

        var stored = await files.EnsureAsync(key, cancellationToken);
        if (stored is null)
        {
            MissingFiles++;
            Report.Add(ReportSections.Files, $"нет объекта `{key}` ({where}) — ссылка не перенесена");
            return null;
        }

        var file = new StoredFile
        {
            StorageKey = key,
            ContentType = FileContentTypes.Of(key) ?? stored.ContentType,
            SizeBytes = stored.Size,
            OriginalName = Path.GetFileName(key),
        };
        _filesByKey[key] = file;
        Db.Files.Add(file);
        return file;
    }

    /// <summary>Внешний адрес — строка <c>files</c> с <c>external_url</c>.</summary>
    public StoredFile ExternalFile(string url)
    {
        if (_filesByKey.TryGetValue(url, out var known))
        {
            return known;
        }

        var file = new StoredFile { ExternalUrl = url, OriginalName = Path.GetFileName(new Uri(url).AbsolutePath) };
        _filesByKey[url] = file;
        Db.Files.Add(file);
        return file;
    }
}
