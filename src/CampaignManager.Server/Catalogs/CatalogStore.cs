using CampaignManager.Contracts.Catalogs;
using CampaignManager.Core;
using CampaignManager.Core.Catalogs;
using CampaignManager.Data;
using CampaignManager.Data.Catalogs;
using CampaignManager.Server.Platform;
using Microsoft.EntityFrameworkCore;

namespace CampaignManager.Server.Catalogs;

/// <summary>
/// Что у справочника своё: таблица, загрузка с детьми, перевод строки в DTO и обратно, проверки, сид.
/// Всё общее (права, ETag, версия, дубль имени, удаление, импорт, синхронизация) — в
/// <see cref="CatalogService{TEntity, TDto}"/>; каталог v1 повторял это семь раз.
/// </summary>
public abstract class CatalogStore<TEntity, TDto>
    where TEntity : CatalogEntry
    where TDto : CatalogItemDto
{
    public abstract CatalogRoute Route { get; }

    /// <summary>Таблица кодов книги: префикс кода проверяется по ней.</summary>
    public abstract CatalogCodeTable Codes { get; }

    /// <summary>
    /// Есть ли у записей код книги. У фонотеки нет: присланный код сервис не читает, и импорт ищет
    /// запись только по имени.
    /// </summary>
    public virtual bool HasCodes => true;

    /// <summary>Как назвать запись в сообщении: «Оружие», «Навык».</summary>
    public abstract string Noun { get; }

    /// <summary>Чем занята запись, когда её нельзя удалить: «оружием или профессиями».</summary>
    public virtual string UsedBy => "другими записями";

    /// <summary>
    /// Кто именно держит запись («оружие «Кольт»», «профессия «Врач»»): имена до пяти штук, чтобы отказ при удалении
    /// говорил, что убрать. У справочников, где это не посчитано, пусто — остаётся общее «используется …».
    /// </summary>
    public virtual Task<IReadOnlyList<string>> UsersOfAsync(CmDbContext db, Guid id, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<string>>([]);

    public abstract DbSet<TEntity> Set(CmDbContext db);

    /// <summary>Запрос с детьми (слоты, заклинания книги, картинки) — для чтения и правки.</summary>
    public virtual IQueryable<TEntity> Query(CmDbContext db) => Set(db);

    /// <summary>Пустая запись: дальше её наполняет <see cref="ApplyAsync"/>.</summary>
    public abstract TEntity New();

    /// <summary>Строки → DTO одним проходом (имена навыков и т. п. — одним запросом на всех).</summary>
    public abstract Task<IReadOnlyList<TDto>> ToDtosAsync(CmDbContext db, IReadOnlyList<TEntity> rows, CancellationToken cancellationToken);

    /// <summary>
    /// Запись для читателя без права правки (игрока): здесь убирается то, что игроку знать нельзя. Пока так только
    /// у бестиария — статблок (<see cref="CreatureStore"/>); у остальных справочников всё открыто.
    /// </summary>
    public virtual TDto ForReader(TDto dto) => dto;

    /// <summary>
    /// Проверить DTO и перенести его в строку. Сначала все проверки (<see cref="ApiProblemException.Invalid"/>),
    /// потом изменения: импорт продолжает со следующей записи, и наполовину изменённая строка ему не нужна.
    /// Общие поля (имя, источник) переносит сервис.
    /// </summary>
    public abstract Task ApplyAsync(CmDbContext db, TDto dto, TEntity entity, CatalogWrite write, CancellationToken cancellationToken);

    /// <summary>Сид книги для «Синхронизировать с правилами»; null — сида нет.</summary>
    public virtual Task<IReadOnlyList<TDto>?> SeedAsync(CmDbContext db, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<TDto>?>(null);

    /// <summary>Есть ли сид — от этого зависит, маппится ли <c>sync</c>.</summary>
    public virtual bool HasSeed => false;

    // ── Проверки, общие для нескольких справочников ──

    protected static List<Era> Eras(List<Era>? eras)
    {
        var result = (eras ?? []).Distinct().Order().ToList();
        return result.Count > 0 ? result : throw ApiProblemException.Invalid("Отметьте хотя бы одну эпоху.");
    }

    protected static List<string> Strings(IEnumerable<string>? values) =>
        (values ?? []).Select(v => v.Trim()).Where(v => v.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

    protected static string? Text(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    protected static int? Range(int? value, int min, int max, string label) =>
        value is null || (value >= min && value <= max)
            ? value
            : throw ApiProblemException.Invalid($"{label}: от {min} до {max}.");
}

/// <summary>Обстоятельства записи: импорт ли это и куда складывать предупреждения по правилам.</summary>
public sealed class CatalogWrite
{
    public bool IsImport { get; init; }

    /// <summary>Сохранилось, но в игре сработает не так, как ждёшь (формула, которую бой не бросит).</summary>
    public List<string> Warnings { get; } = [];
}
