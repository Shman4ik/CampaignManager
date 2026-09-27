using CampaignManager.Web.Components.Features.Spells.Model;
using CampaignManager.Web.Utilities.DataBase;
using CampaignManager.Web.Utilities.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace CampaignManager.Web.Components.Features.Spells.Services;

/// <summary>
///     Справочник заклинаний. Читают все вошедшие, правят только Хранитель и администратор —
///     проверка стоит в каждом методе записи, а не только в разметке страницы.
/// </summary>
public sealed class SpellService(
    IDbContextFactory<AppDbContext> dbContextFactory,
    IMemoryCache cache,
    IdentityService identityService,
    ILogger<SpellService> logger)
{
    private const string SpellsKey = "AllSpells";

    /// <summary>
    ///     Gets all spells.
    /// </summary>
    public Task<List<Spell>> GetAllSpellsAsync() =>
        CrudServiceHelper.GetAllCachedAsync<Spell>(dbContextFactory, cache, SpellsKey, logger);

    /// <summary>
    ///     Adds a new spell.
    /// </summary>
    public async Task<bool> AddSpellAsync(Spell spell)
    {
        await identityService.EnsureKeeperAsync("добавление заклинания в справочник");
        if (string.IsNullOrWhiteSpace(spell.Name)) return false;
        return await CrudServiceHelper.CreateAsync(dbContextFactory, cache, SpellsKey, spell, logger) is not null;
    }

    /// <summary>
    ///     Updates an existing spell.
    /// </summary>
    public async Task<bool> UpdateSpellAsync(Spell spell)
    {
        await identityService.EnsureKeeperAsync("изменение заклинания в справочнике");
        return await CrudServiceHelper.UpdateAsync(dbContextFactory, cache, SpellsKey, spell, logger);
    }

    /// <summary>
    ///     Deletes a spell by ID.
    /// </summary>
    public async Task<bool> DeleteSpellAsync(Guid id)
    {
        await identityService.EnsureKeeperAsync("удаление заклинания из справочника");
        return await CrudServiceHelper.DeleteAsync<Spell>(dbContextFactory, cache, SpellsKey, id, logger);
    }
}
