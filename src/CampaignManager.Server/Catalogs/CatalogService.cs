using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using CampaignManager.Contracts.Catalogs;
using CampaignManager.Core.Catalogs;
using CampaignManager.Data;
using CampaignManager.Data.Catalogs;
using CampaignManager.Server.Access;
using CampaignManager.Server.Platform;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CampaignManager.Server.Catalogs;

/// <summary>
/// Общий сервис справочника. Читать — любой вошедший, писать — Хранитель: каждый метод записи зовёт
/// <see cref="AccessPolicy.ForCatalogAsync"/> и <c>Demand</c> (в v1 четыре справочника права не
/// проверяли вовсе). Список целиком — справочники маленькие; ETag по версиям строк.
/// </summary>
public sealed class CatalogService<TEntity, TDto>(
    CmDbContext db,
    AccessPolicy access,
    CurrentUser currentUser,
    CatalogStore<TEntity, TDto> store,
    ILogger<CatalogService<TEntity, TDto>> logger)
    where TEntity : CatalogEntry
    where TDto : CatalogItemDto
{
    private const int MaxNameLength = 200;

    public CatalogStore<TEntity, TDto> Store => store;

    /// <summary>Список по имени и ETag: хеш id и версий строк, права (кнопки правки зависят от них) и сборка контрактов.</summary>
    public async Task<(CatalogList<TDto> List, string ETag)> ListAsync(CancellationToken cancellationToken)
    {
        var rights = await access.ForCatalogAsync(cancellationToken).Demand(Operation.Read);
        var rows = await store.Query(db).AsNoTracking().OrderBy(e => e.Name).ToListAsync(cancellationToken);
        var items = await store.ToDtosAsync(db, rows, cancellationToken);
        if (!rights.CanEdit)
        {
            items = [.. items.Select(store.ForReader)];
        }

        return (new CatalogList<TDto>(items, rights.CanEdit), ETagOf(rows, rights.CanEdit));
    }

    public async Task<TDto> CreateAsync(TDto dto, CancellationToken cancellationToken)
    {
        await access.ForCatalogAsync(cancellationToken).Demand(Operation.Edit);
        var entity = store.New();
        entity.CreatedById = (await currentUser.GetAsync(cancellationToken))?.Id;
        await ApplyAsync(dto, entity, isNew: true, new CatalogWrite(), cancellationToken);
        store.Set(db).Add(entity);
        await SaveAsync(cancellationToken);
        return await ReadAsync(entity.Id, cancellationToken);
    }

    /// <param name="ifMatch">Версия, которую правили (<c>If-Match</c>). Другая — 409, а не перезапись.</param>
    public async Task<TDto> UpdateAsync(Guid id, TDto dto, uint? ifMatch, CancellationToken cancellationToken)
    {
        await access.ForCatalogAsync(cancellationToken).Demand(Operation.Edit);
        if (ifMatch is not { } version)
        {
            throw ApiProblemException.VersionRequired();
        }

        var entity = await store.Query(db).SingleOrDefaultAsync(e => e.Id == id, cancellationToken)
            ?? throw AccessDeniedException.NotFound();
        if (entity.Version != version)
        {
            throw ApiProblemException.Stale();
        }

        await ApplyAsync(dto, entity, isNew: false, new CatalogWrite(), cancellationToken);
        await SaveAsync(cancellationToken);
        return await ReadAsync(id, cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        await access.ForCatalogAsync(cancellationToken).Demand(Operation.Delete);
        var entity = await store.Query(db).SingleOrDefaultAsync(e => e.Id == id, cancellationToken)
            ?? throw AccessDeniedException.NotFound();
        store.Set(db).Remove(entity);
        await SaveAsync(cancellationToken, id);
    }

    /// <summary>Кто держит запись — до удаления: подтверждение называет их, а не отказ после него.</summary>
    public async Task<CatalogUsage> UsageAsync(Guid id, CancellationToken cancellationToken)
    {
        await access.ForCatalogAsync(cancellationToken).Demand(Operation.Delete);
        var users = await UsersAsync(id, cancellationToken);
        return users.Count == 0
            ? CatalogUsage.None
            : new CatalogUsage(users.Count, [.. users.Take(5)], store.UsersBlockDelete, store.UsersNote);
    }

    public async Task<CatalogFile<TDto>> ExportAsync(CancellationToken cancellationToken)
    {
        var (list, _) = await ListAsync(cancellationToken);
        return new CatalogFile<TDto>(store.Route.Name, list.Items);
    }

    /// <summary>
    /// Импорт файла обмена (<c>CatalogExchange</c>): ключ — код, без него — имя без учёта регистра.
    /// Найденная запись пропускается или перезаписывается; ошибка одной записи не останавливает
    /// остальные. Пробный прогон ничего не пишет, но проходит те же проверки.
    /// </summary>
    public async Task<CatalogImportReport> ImportAsync(CatalogFile<TDto> file, bool overwrite, bool dryRun, CancellationToken cancellationToken)
    {
        await access.ForCatalogAsync(cancellationToken).Demand(Operation.Edit);
        if (!string.Equals(file.Catalog, store.Route.Name, StringComparison.OrdinalIgnoreCase))
        {
            throw ApiProblemException.Invalid(
                $"Это файл справочника «{file.Catalog}», а импорт идёт в «{store.Route.Name}».");
        }

        return await MergeAsync(file.Items, overwrite, dryRun, byCodeOnly: false, cancellationToken);
    }

    /// <summary>
    /// «Синхронизировать с правилами»: апсерт сида по коду. Запись с кодом обновляется (имя тоже —
    /// коды постоянны, имена меняются), без него — создаётся. Самодельные (код null) не трогаются,
    /// кроме одного случая: самодельная с тем же именем, что у книжной, получает её код.
    /// </summary>
    public async Task<CatalogImportReport> SyncAsync(bool dryRun, CancellationToken cancellationToken)
    {
        await access.ForCatalogAsync(cancellationToken).Demand(Operation.Edit);
        var seed = await store.SeedAsync(db, cancellationToken)
            ?? throw AccessDeniedException.NotFound();
        return await MergeAsync(seed, overwrite: true, dryRun, byCodeOnly: true, cancellationToken);
    }

    private async Task<CatalogImportReport> MergeAsync(
        IReadOnlyList<TDto> incoming,
        bool overwrite,
        bool dryRun,
        bool byCodeOnly,
        CancellationToken cancellationToken)
    {
        var existing = await store.Query(db).ToListAsync(cancellationToken);
        var byCode = existing.Where(e => e.Code is not null).ToDictionary(e => e.Code!, StringComparer.Ordinal);
        var byName = existing.ToDictionary(e => NameKey(e.Name), StringComparer.Ordinal);
        var author = (await currentUser.GetAsync(cancellationToken))?.Id;

        List<CatalogImportLine> lines = [];
        List<string> warnings = [];
        int created = 0, updated = 0, skipped = 0, failed = 0;
        foreach (var dto in incoming)
        {
            var name = dto.Name?.Trim() ?? "";
            var code = !store.HasCodes || string.IsNullOrWhiteSpace(dto.Code) ? null : dto.Code.Trim();
            var target = code is not null && byCode.TryGetValue(code, out var withCode) ? withCode
                : byName.GetValueOrDefault(NameKey(name)) is { } withName
                  && (!byCodeOnly || withName.Code is null) && (code is null || withName.Code is null || withName.Code == code)
                    ? withName
                    : null;

            if (target is not null && !overwrite)
            {
                skipped++;
                lines.Add(new CatalogImportLine(name, CatalogImportOutcome.Skipped, "уже есть"));
                continue;
            }

            var write = new CatalogWrite { IsImport = true };
            try
            {
                var isNew = target is null;
                var entity = target ?? store.New();
                var before = isNew ? null : CatalogChanges.Snapshot((await store.ToDtosAsync(db, [entity], cancellationToken))[0]);
                var oldKey = NameKey(entity.Name);
                if (isNew)
                {
                    entity.CreatedById = author;
                }
                else if (code is not null && entity.Code is null)
                {
                    entity.Code = ValidCode(code); // самодельная с книжным именем получает код книги
                }

                await ApplyAsync(dto, entity, isNew, write, cancellationToken, byName);
                if (isNew)
                {
                    store.Set(db).Add(entity);
                    created++;
                }
                else
                {
                    updated++;
                    byName.Remove(oldKey);
                }

                byName[NameKey(entity.Name)] = entity;
                if (entity.Code is not null)
                {
                    byCode[entity.Code] = entity;
                }

                List<string> notes = [];
                if (before is not null)
                {
                    var changed = CatalogChanges.Diff(before, CatalogChanges.Snapshot((await store.ToDtosAsync(db, [entity], cancellationToken))[0]));
                    notes.Add(changed.Count > 0 ? $"изменено: {string.Join(", ", changed)}" : "без изменений");
                }

                notes.AddRange(write.Warnings);
                lines.Add(new CatalogImportLine(entity.Name,
                    isNew ? CatalogImportOutcome.Created : CatalogImportOutcome.Updated,
                    notes.Count > 0 ? string.Join(". ", notes) : null));
                warnings.AddRange(write.Warnings.Select(w => $"{entity.Name}: {w}"));
            }
            catch (ApiProblemException problem)
            {
                failed++;
                lines.Add(new CatalogImportLine(name.Length > 0 ? name : "(без имени)", CatalogImportOutcome.Failed, problem.Message));
            }
        }

        if (!dryRun)
        {
            await SaveAsync(cancellationToken);
        }

        return new CatalogImportReport(dryRun, created, updated, skipped, failed, lines, warnings);
    }

    /// <summary>Общие поля и проверка имени, потом своё у справочника.</summary>
    private async Task ApplyAsync(
        TDto dto,
        TEntity entity,
        bool isNew,
        CatalogWrite write,
        CancellationToken cancellationToken,
        Dictionary<string, TEntity>? batchNames = null)
    {
        var name = dto.Name?.Trim() ?? "";
        if (name.Length == 0)
        {
            throw ApiProblemException.Invalid("Нужно название.");
        }

        if (name.Length > MaxNameLength)
        {
            throw ApiProblemException.Invalid($"Название длиннее {MaxNameLength} знаков.");
        }

        // Имя уникально без учёта регистра (индекс lower(name)). В импорте — сверка и с тем, что
        // уже добавлено в этом же файле, но ещё не записано.
        var key = NameKey(name);
        var taken = batchNames is not null
            ? batchNames.TryGetValue(key, out var other) && other != entity
            : await store.Set(db).AnyAsync(e => e.Id != entity.Id && e.Name.ToLower() == name.ToLower(), cancellationToken);
        if (taken)
        {
            throw ApiProblemException.Duplicate($"{store.Noun} «{name}» уже есть в справочнике. Выберите другое название.");
        }

        string? code = null;
        if (isNew && store.HasCodes && !string.IsNullOrWhiteSpace(dto.Code))
        {
            code = ValidCode(dto.Code.Trim());
            if (await store.Set(db).AnyAsync(e => e.Code == code, cancellationToken))
            {
                throw ApiProblemException.Duplicate($"Код «{code}» уже занят другой записью.");
            }
        }

        await store.ApplyAsync(db, dto, entity, write, cancellationToken);

        entity.Name = name;
        entity.Source = string.IsNullOrWhiteSpace(dto.Source) ? (write.IsImport ? entity.Source : null) : dto.Source.Trim();
        if (isNew)
        {
            entity.Code = code;
        }

        if (!isNew)
        {
            // Правка только детей (слоты, заклинания книги, картинки) корневую строку не меняет —
            // а версия и updated_at должны сдвинуться, и проверка If-Match должна сработать.
            db.Entry(entity).Property(e => e.Name).IsModified = true;
        }
    }

    private string ValidCode(string code) =>
        code.StartsWith(store.Codes.Prefix, StringComparison.Ordinal) && code.Length > store.Codes.Prefix.Length
            && code.All(c => c is (>= 'a' and <= 'z') or (>= '0' and <= '9') or '-' or '.')
            ? code
            : throw ApiProblemException.Invalid($"Код «{code}» не похож на код справочника: нужно «{store.Codes.Prefix}…» в нижнем регистре.");

    private async Task<TDto> ReadAsync(Guid id, CancellationToken cancellationToken)
    {
        db.ChangeTracker.Clear();
        var row = await store.Query(db).AsNoTracking().SingleAsync(e => e.Id == id, cancellationToken);
        return (await store.ToDtosAsync(db, [row], cancellationToken))[0];
    }

    /// <summary>Сохранить и перевести нарушения базы в понятный отказ.</summary>
    private async Task SaveAsync(CancellationToken cancellationToken, Guid? entityId = null)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.ForeignKeyViolation })
        {
            logger.LogInformation(ex, "Запись справочника {Catalog} занята ссылками", store.Route.Name);
            var users = await UsersAsync(entityId, cancellationToken);
            var named = users.Count == 0 ? "" : $" Это: {string.Join(", ", users.Take(5))}{(users.Count > 5 ? $" и ещё {users.Count - 5}" : "")}.";
            throw ApiProblemException.InUse($"{store.Noun} используется {store.UsedBy} — сначала уберите ссылки.{named}");
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // Гонка двух записей с одним именем: проверка выше прошла у обоих.
            logger.LogInformation(ex, "Дубль в справочнике {Catalog}", store.Route.Name);
            throw ApiProblemException.Duplicate($"{store.Noun} с таким названием или кодом уже есть в справочнике.");
        }
    }

    private async Task<IReadOnlyList<string>> UsersAsync(Guid? id, CancellationToken cancellationToken)
    {
        if (id is not { } key)
        {
            return [];
        }

        db.ChangeTracker.Clear(); // упавшее удаление ещё висит в трекере
        return await store.UsersOfAsync(db, key, cancellationToken);
    }

    private static string NameKey(string name) => CatalogCodeTable.NormalizeName(name);

    /// <summary>
    /// Сборка контрактов: новая версия сервера может отдавать те же строки в другом виде (новое поле DTO),
    /// и браузер по старому ETag получил бы 304 и показал прежний ответ.
    /// </summary>
    private static readonly string BuildId = typeof(TDto).Assembly.ManifestModule.ModuleVersionId.ToString("N");

    private static string ETagOf(IReadOnlyList<TEntity> rows, bool canEdit)
    {
        var text = new StringBuilder(rows.Count * 48).Append(BuildId).Append(canEdit ? 'e' : 'r');
        foreach (var row in rows.OrderBy(r => r.Id))
        {
            text.Append(row.Id.ToString("N")).Append(':').Append(row.Version.ToString(CultureInfo.InvariantCulture)).Append(';');
        }

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(text.ToString()));
        return $"\"{Convert.ToHexStringLower(hash.AsSpan(0, 16))}\"";
    }
}
