using CampaignManager.Web.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace CampaignManager.Web.SpellComponents;

/// <summary>
///     Service for managing spells data.
/// </summary>
public class SpellService(
    IDbContextFactory<AppDbContext> dbContextFactory,
    IMemoryCache cache,
    IWebHostEnvironment environment,
    ILogger<SpellService> logger)
{
    private const string SpellsKey = "AllSpells";


    /// <summary>
    ///     Gets all spells.
    /// </summary>
    /// <returns>List of all spells.</returns>
    public async Task<List<Spell>> GetAllSpellsAsync()
    {
        if (cache.TryGetValue(SpellsKey, out List<Spell> cachedSpells)) return cachedSpells;

        using var dbContext = await dbContextFactory.CreateDbContextAsync();
        var spells = await dbContext.Spells.OrderBy(s => s.Name).ToListAsync();

        cache.Set(SpellsKey, spells, TimeSpan.FromMinutes(10));
        return spells;
    }

    /// <summary>
    ///     Gets a spell by name.
    /// </summary>
    /// <param name="name">The name of the spell to retrieve.</param>
    /// <returns>The spell if found, null otherwise.</returns>
    public async Task<Spell?> GetSpellByNameAsync(string name)
    {
        using var dbContext = await dbContextFactory.CreateDbContextAsync();
        return await dbContext.Spells
            .FirstOrDefaultAsync(s => s.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    ///     Gets a spell by ID.
    /// </summary>
    /// <param name="id">The ID of the spell to retrieve.</param>
    /// <returns>The spell if found, null otherwise.</returns>
    public async Task<Spell?> GetSpellByIdAsync(Guid id)
    {
        using var dbContext = await dbContextFactory.CreateDbContextAsync();
        return await dbContext.Spells.FindAsync(id);
    }

    /// <summary>
    ///     Adds a new spell.
    /// </summary>
    /// <param name="spell">The spell to add.</param>
    /// <returns>True if successful, false otherwise.</returns>
    public async Task<bool> AddSpellAsync(Spell spell)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(spell.Name)) return false;

            using var dbContext = await dbContextFactory.CreateDbContextAsync();

            // Check if a spell with this name already exists
            if (await dbContext.Spells.AnyAsync(s => s.Name.Equals(spell.Name, StringComparison.OrdinalIgnoreCase))) return false;

            spell.Id = Guid.NewGuid();
            await dbContext.Spells.AddAsync(spell);
            await dbContext.SaveChangesAsync();

            ClearCache();
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error adding spell {SpellName}", spell.Name);
            return false;
        }
    }

    /// <summary>
    ///     Updates an existing spell.
    /// </summary>
    /// <param name="spell">The updated spell.</param>
    /// <returns>True if successful, false otherwise.</returns>
    public async Task<bool> UpdateSpellAsync(Spell spell)
    {
        try
        {
            using var dbContext = await dbContextFactory.CreateDbContextAsync();
            var existingSpell = await dbContext.Spells.FindAsync(spell.Id);

            if (existingSpell == null) return false;

            // Update the entity properties
            dbContext.Entry(existingSpell).CurrentValues.SetValues(spell);
            await dbContext.SaveChangesAsync();

            ClearCache();
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error updating spell {SpellName}", spell.Name);
            return false;
        }
    }

    /// <summary>
    ///     Deletes a spell by ID.
    /// </summary>
    /// <param name="id">The ID of the spell to delete.</param>
    /// <returns>True if successful, false otherwise.</returns>
    public async Task<bool> DeleteSpellAsync(Guid id)
    {
        try
        {
            using var dbContext = await dbContextFactory.CreateDbContextAsync();
            var spell = await dbContext.Spells.FindAsync(id);

            if (spell == null) return false;

            dbContext.Spells.Remove(spell);
            await dbContext.SaveChangesAsync();

            ClearCache();
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deleting spell with ID {SpellId}", id);
            return false;
        }
    }

    /// <summary>
    ///     Clears the spell cache.
    /// </summary>
    private void ClearCache()
    {
        cache.Remove(SpellsKey);
    }
}