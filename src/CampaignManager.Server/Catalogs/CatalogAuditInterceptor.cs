using System.Text.Json;
using CampaignManager.Core.Admin;
using CampaignManager.Data.Admin;
using CampaignManager.Data.Catalogs;
using CampaignManager.Server.Access;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace CampaignManager.Server.Catalogs;

/// <summary>
/// История правок справочников — только запись снимка «после» (<c>cm.audit_log</c>), на случай «кто это
/// сломал». Интерфейса истории нет (D6.1): в v1 «сравнить» показывал две JSON-простыни, а «откатить» не
/// работал. Строка журнала добавляется в то же сохранение, а не вторым <c>SaveChanges</c>; перехватчик
/// scoped (в v1 — синглтон с общим списком и гонкой), автор — <see cref="CurrentUser"/>.
/// Снимок — колонки корневой строки; дети (слоты, заклинания книги, картинки) в него не входят.
/// </summary>
public sealed class CatalogAuditInterceptor(IServiceProvider services) : SaveChangesInterceptor
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() },
    };

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is { } context)
        {
            var changed = context.ChangeTracker.Entries<CatalogEntry>()
                .Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
                .ToList();
            if (changed.Count > 0)
            {
                // CurrentUser берётся при сохранении, а не в конструкторе: он сам читает cm.users через
                // CmDbContext, и зависимость в конструкторе замкнулась бы в круг.
                var actor = (await services.GetRequiredService<CurrentUser>().GetAsync(cancellationToken))?.Id;
                foreach (var entry in changed)
                {
                    context.Add(Entry(entry, actor));
                }
            }
        }

        return await base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result) =>
        eventData.Context?.ChangeTracker.Entries<CatalogEntry>().Any(e => e.State != EntityState.Unchanged) == true
            ? throw new InvalidOperationException("Справочники сохраняются только асинхронно: автору записи нужен запрос к базе.")
            : base.SavingChanges(eventData, result);

    private static AuditLogEntry Entry(EntityEntry<CatalogEntry> entry, Guid? actor)
    {
        var deleted = entry.State == EntityState.Deleted;
        return new AuditLogEntry
        {
            EntityType = entry.Metadata.ClrType.Name,
            EntityId = entry.Entity.Id,
            Action = entry.State switch
            {
                EntityState.Added => AuditAction.Created,
                EntityState.Deleted => AuditAction.Deleted,
                _ => AuditAction.Updated,
            },
            ActorId = actor,
            Snapshot = deleted ? null : Snapshot(entry),
        };
    }

    private static JsonDocument Snapshot(EntityEntry<CatalogEntry> entry)
    {
        var values = entry.Properties
            .Where(p => !p.Metadata.IsShadowProperty() && p.Metadata.Name != nameof(CatalogEntry.Version))
            .ToDictionary(p => JsonNamingPolicy.CamelCase.ConvertName(p.Metadata.Name), p => p.CurrentValue);
        return JsonSerializer.SerializeToDocument(values, Json);
    }
}
