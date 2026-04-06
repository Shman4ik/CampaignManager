using System.Text.Json;
using CampaignManager.Web.Components.Features.Bestiary.Model;
using CampaignManager.Web.Components.Features.Characters.Model;
using CampaignManager.Web.Components.Features.Items.Model;
using CampaignManager.Web.Components.Features.Skills.Model;
using CampaignManager.Web.Components.Features.Spells.Model;
using CampaignManager.Web.Components.Features.Weapons.Model;
using CampaignManager.Web.Components.Features.Wiki.Model;
using CampaignManager.Web.Model;
using CampaignManager.Web.Utilities.DataBase;
using Microsoft.EntityFrameworkCore;

namespace CampaignManager.Web.Components.Features.Wiki.Services;

public sealed class WikiHistoryService(
    IDbContextFactory<AppDbContext> dbContextFactory,
    ILogger<WikiHistoryService> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static readonly Dictionary<string, Type> EntityTypeMap = new()
    {
        ["Weapon"] = typeof(Weapon),
        ["Spell"] = typeof(Spell),
        ["SkillModel"] = typeof(SkillModel),
        ["Creature"] = typeof(Creature),
        ["Item"] = typeof(Item),
        ["Occupation"] = typeof(Occupation)
    };

    public async Task<List<EditHistoryEntry>> GetHistoryAsync(string entityType, Guid entityId, int limit = 50)
    {
        try
        {
            await using var db = await dbContextFactory.CreateDbContextAsync();
            return await db.EditHistoryEntries
                .Where(e => e.EntityType == entityType && e.EntityId == entityId)
                .OrderByDescending(e => e.CreatedAt)
                .Take(limit)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving history for {EntityType} {EntityId}", entityType, entityId);
            return [];
        }
    }

    public async Task<bool> RevertAsync(Guid entryId)
    {
        try
        {
            await using var db = await dbContextFactory.CreateDbContextAsync();
            var entry = await db.EditHistoryEntries.FindAsync(entryId);
            if (entry is null)
            {
                logger.LogWarning("Edit history entry {Id} not found", entryId);
                return false;
            }

            // For delete, restore from snapshot; for update, restore from previous snapshot
            var jsonToRestore = entry.Action == EditAction.Deleted
                ? entry.PreviousSnapshotJson
                : entry.PreviousSnapshotJson;

            if (jsonToRestore is null)
            {
                logger.LogWarning("No snapshot available to revert entry {Id}", entryId);
                return false;
            }

            if (!EntityTypeMap.TryGetValue(entry.EntityType, out var clrType))
            {
                logger.LogWarning("Unknown entity type {EntityType} for revert", entry.EntityType);
                return false;
            }

            var restoredEntity = JsonSerializer.Deserialize(jsonToRestore, clrType, JsonOptions) as BaseDataBaseEntity;
            if (restoredEntity is null)
            {
                logger.LogWarning("Failed to deserialize snapshot for entry {Id}", entryId);
                return false;
            }

            if (entry.Action == EditAction.Deleted)
            {
                // Re-add the entity
                db.Add(restoredEntity);
            }
            else
            {
                // Update existing entity
                var existing = await db.FindAsync(clrType, restoredEntity.Id);
                if (existing is null)
                {
                    logger.LogWarning("Entity {EntityType} {EntityId} not found for revert", entry.EntityType, restoredEntity.Id);
                    return false;
                }

                db.Entry(existing).CurrentValues.SetValues(restoredEntity);
            }

            restoredEntity.LastUpdated = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync();

            logger.LogInformation("Reverted {EntityType} {EntityId} from entry {EntryId}",
                entry.EntityType, entry.EntityId, entryId);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error reverting entry {EntryId}", entryId);
            return false;
        }
    }
}
