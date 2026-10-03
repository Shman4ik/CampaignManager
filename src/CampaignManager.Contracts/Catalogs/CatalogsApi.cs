namespace CampaignManager.Contracts.Catalogs;

/// <summary>
/// Справочник на сервере: адрес и имя в файле обмена. Маршруты одинаковы у всех справочников:
/// <c>GET</c> список, <c>POST</c> создать, <c>PUT|DELETE {id}</c>, <c>POST import</c>, <c>GET export</c>,
/// <c>POST sync</c> (только где есть сид книги).
/// </summary>
public sealed record CatalogRoute(string Name)
{
    public string Base => $"{ApiRoutes.Prefix}/catalogs/{Name}";

    public string ItemPattern => Base + "/{id:guid}";

    public string Import => Base + "/import";

    public string Export => Base + "/export";

    public string Sync => Base + "/sync";

    public string Item(Guid id) => $"{Base}/{id}";

    public string Usage(Guid id) => $"{Item(id)}/usage";
}

public static class CatalogsRoutes
{
    public static readonly CatalogRoute Skills = new("skills");
    public static readonly CatalogRoute Occupations = new("occupations");
    public static readonly CatalogRoute Weapons = new("weapons");
    public static readonly CatalogRoute Spells = new("spells");
    public static readonly CatalogRoute Books = new("books");
    public static readonly CatalogRoute Items = new("items");
    public static readonly CatalogRoute Creatures = new("creatures");

    /// <summary>Параметры импорта в адресе: тело — сам файл обмена.</summary>
    public const string OverwriteQuery = "overwrite";

    public const string DryRunQuery = "dryRun";
}

/// <summary>
/// Справочник: читать — любой вошедший, писать — Хранитель (<c>AccessPolicy.ForCatalogAsync</c>).
/// Список приходит целиком (справочники маленькие — до ~320 строк): поиск, фильтры и страницы — на
/// клиенте, у всех справочников одинаково.
/// </summary>
public interface ICatalogApi<T>
    where T : CatalogItemDto
{
    CatalogRoute Route { get; }

    Task<CatalogList<T>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>Создать. Дубль имени или кода — <c>ApiException</c> 409 <c>duplicate</c>.</summary>
    Task<T> CreateAsync(T item, CancellationToken cancellationToken = default);

    /// <summary>
    /// Записать правку. <see cref="CatalogItemDto.Version"/> уходит в <c>If-Match</c>: запись изменили
    /// на другом устройстве — 409 <c>stale</c>, а не молчаливая перезапись.
    /// </summary>
    Task<T> UpdateAsync(T item, CancellationToken cancellationToken = default);

    /// <summary>Удалить. На запись ссылаются — 409 <c>in-use</c>.</summary>
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Кто держит запись, — до удаления, чтобы подтверждение сказало об этом, а не отказ после него. По умолчанию
    /// никто (реализация без сведений); сервер отвечает по справочнику.
    /// </summary>
    Task<CatalogUsage> UsageAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(CatalogUsage.None);

    /// <summary>
    /// Импорт файла обмена как есть (<see cref="CatalogFile{T}"/>): существующая запись (по коду, иначе
    /// по имени) пропускается или перезаписывается; <paramref name="dryRun"/> — только отчёт.
    /// </summary>
    Task<CatalogImportReport> ImportAsync(Stream file, bool overwrite, bool dryRun, CancellationToken cancellationToken = default);

    /// <summary>
    /// «Синхронизировать с правилами»: апсерт сида книги по коду. Самодельные записи (без кода) не
    /// трогает. Есть только у справочников с сидом.
    /// </summary>
    Task<CatalogImportReport> SyncAsync(bool dryRun, CancellationToken cancellationToken = default);
}
