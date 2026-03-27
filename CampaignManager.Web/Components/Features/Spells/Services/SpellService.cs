using CampaignManager.Web.Components.Features.Spells.Model;
using CampaignManager.Web.Utilities.DataBase;
using CampaignManager.Web.Utilities.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace CampaignManager.Web.Components.Features.Spells.Services;

/// <summary>
///     Service for managing spells data.
/// </summary>
public sealed class SpellService(
    IDbContextFactory<AppDbContext> dbContextFactory,
    IMemoryCache cache,
    ILogger<SpellService> logger)
{
    private const string SpellsKey = "AllSpells";

    /// <summary>
    ///     Gets all spells.
    /// </summary>
    public Task<List<Spell>> GetAllSpellsAsync() =>
        CrudServiceHelper.GetAllCachedAsync<Spell>(dbContextFactory, cache, SpellsKey, logger);

    /// <summary>
    ///     Gets a spell by name.
    /// </summary>
    public async Task<Spell?> GetSpellByNameAsync(string name)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        return await dbContext.Spells
            .FirstOrDefaultAsync(s => s.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    ///     Gets a spell by ID.
    /// </summary>
    public Task<Spell?> GetSpellByIdAsync(Guid id) =>
        CrudServiceHelper.GetByIdAsync<Spell>(dbContextFactory, id, logger);

    /// <summary>
    ///     Adds a new spell.
    /// </summary>
    public async Task<bool> AddSpellAsync(Spell spell)
    {
        if (string.IsNullOrWhiteSpace(spell.Name)) return false;
        return await CrudServiceHelper.CreateAsync(dbContextFactory, cache, SpellsKey, spell, logger) is not null;
    }

    /// <summary>
    ///     Updates an existing spell.
    /// </summary>
    public Task<bool> UpdateSpellAsync(Spell spell) =>
        CrudServiceHelper.UpdateAsync(dbContextFactory, cache, SpellsKey, spell, logger);

    /// <summary>
    ///     Deletes a spell by ID.
    /// </summary>
    public Task<bool> DeleteSpellAsync(Guid id) =>
        CrudServiceHelper.DeleteAsync<Spell>(dbContextFactory, cache, SpellsKey, id, logger);
}
