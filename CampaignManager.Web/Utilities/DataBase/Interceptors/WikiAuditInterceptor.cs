using System.Security.Claims;
using System.Text.Json;
using CampaignManager.Web.Components.Features.Bestiary.Model;
using CampaignManager.Web.Components.Features.Characters.Model;
using CampaignManager.Web.Components.Features.Items.Model;
using CampaignManager.Web.Components.Features.Skills.Model;
using CampaignManager.Web.Components.Features.Spells.Model;
using CampaignManager.Web.Components.Features.Weapons.Model;
using CampaignManager.Web.Components.Features.Wiki.Model;
using CampaignManager.Web.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace CampaignManager.Web.Utilities.DataBase.Interceptors;

public sealed class WikiAuditInterceptor(IHttpContextAccessor httpContextAccessor) : SaveChangesInterceptor
{
    private static readonly HashSet<Type> AuditedTypes =
    [
        typeof(Weapon),
        typeof(Spell),
        typeof(SkillModel),
        typeof(Creature),
        typeof(Item),
        typeof(Occupation)
    ];

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private List<AuditEntry>? _pendingEntries;

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is null)
            return await base.SavingChangesAsync(eventData, result, cancellationToken);

        var (editorEmail, editorName) = GetCurrentEditor();
        _pendingEntries = [];

        foreach (var entry in eventData.Context.ChangeTracker.Entries())
        {
            if (!AuditedTypes.Contains(entry.Entity.GetType()))
                continue;

            if (entry.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted))
                continue;

            var entity = entry.Entity as BaseDataBaseEntity;
            if (entity is null)
                continue;

            var entityType = entry.Entity.GetType().Name;

            var action = entry.State switch
            {
                EntityState.Added => EditAction.Created,
                EntityState.Modified => EditAction.Updated,
                EntityState.Deleted => EditAction.Deleted,
                _ => EditAction.Updated
            };

            string? snapshotJson = null;
            string? previousSnapshotJson = null;

            if (entry.State == EntityState.Deleted)
            {
                previousSnapshotJson = SerializeEntity(entry.Entity);
            }
            else if (entry.State == EntityState.Modified)
            {
                snapshotJson = SerializeEntity(entry.Entity);
                previousSnapshotJson = SerializeOriginalValues(entry);
            }
            else // Added
            {
                snapshotJson = SerializeEntity(entry.Entity);
            }

            _pendingEntries.Add(new AuditEntry
            {
                EntityType = entityType,
                EntityId = entity.Id,
                Action = action,
                EditorEmail = editorEmail,
                EditorName = editorName,
                SnapshotJson = snapshotJson,
                PreviousSnapshotJson = previousSnapshotJson
            });
        }

        return await base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        if (_pendingEntries is { Count: > 0 } && eventData.Context is not null)
        {
            foreach (var pending in _pendingEntries)
            {
                var historyEntry = new EditHistoryEntry
                {
                    EntityType = pending.EntityType,
                    EntityId = pending.EntityId,
                    Action = pending.Action,
                    EditorEmail = pending.EditorEmail,
                    EditorName = pending.EditorName,
                    SnapshotJson = pending.SnapshotJson,
                    PreviousSnapshotJson = pending.PreviousSnapshotJson
                };
                historyEntry.Init();

                eventData.Context.Set<EditHistoryEntry>().Add(historyEntry);
            }

            await eventData.Context.SaveChangesAsync(cancellationToken);
            _pendingEntries = null;
        }

        return await base.SavedChangesAsync(eventData, result, cancellationToken);
    }

    private (string Email, string Name) GetCurrentEditor()
    {
        var user = httpContextAccessor.HttpContext?.User;
        var email = user?.FindFirst(ClaimTypes.Email)?.Value ?? "system";
        var name = user?.Identity?.Name ?? "System";
        return (email, name);
    }

    private static string? SerializeEntity(object entity)
    {
        try
        {
            return JsonSerializer.Serialize(entity, entity.GetType(), JsonOptions);
        }
        catch
        {
            return null;
        }
    }

    private static string? SerializeOriginalValues(Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry entry)
    {
        try
        {
            var dict = new Dictionary<string, object?>();
            foreach (var prop in entry.OriginalValues.Properties)
            {
                dict[prop.Name] = entry.OriginalValues[prop];
            }
            return JsonSerializer.Serialize(dict, JsonOptions);
        }
        catch
        {
            return null;
        }
    }

    private sealed class AuditEntry
    {
        public string EntityType { get; init; } = string.Empty;
        public Guid EntityId { get; init; }
        public EditAction Action { get; init; }
        public string EditorEmail { get; init; } = string.Empty;
        public string EditorName { get; init; } = string.Empty;
        public string? SnapshotJson { get; init; }
        public string? PreviousSnapshotJson { get; init; }
    }
}
