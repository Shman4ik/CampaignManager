using CampaignManager.Web.Scenarios.Models;
using CampaignManager.Web.Services;
using Microsoft.EntityFrameworkCore;

namespace CampaignManager.Web.Scenarios.Services;

/// <summary>
/// Service for managing NPCs in scenarios
/// </summary>
public sealed class ScenarioNpcService(
    IDbContextFactory<AppDbContext> dbContextFactory,
    ILogger<ScenarioNpcService> logger)
{
    /// <summary>
    /// Gets all NPCs for a specific scenario
    /// </summary>
    /// <param name="scenarioId">The ID of the scenario</param>
    /// <returns>A list of NPCs in the scenario</returns>
    public async Task<List<ScenarioNpc>> GetNpcsByScenarioAsync(Guid scenarioId)
    {
        try
        {
            using AppDbContext dbContext = await dbContextFactory.CreateDbContextAsync();
            return await dbContext.ScenarioNpcs
                .Where(n => n.ScenarioId == scenarioId)
                .OrderBy(n => n.Name)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving NPCs for scenario {ScenarioId}", scenarioId);
            return [];
        }
    }
    
    /// <summary>
    /// Gets an NPC by its ID
    /// </summary>
    /// <param name="id">The ID of the NPC</param>
    /// <returns>The NPC if found, null otherwise</returns>
    public async Task<ScenarioNpc?> GetNpcByIdAsync(Guid id)
    {
        try
        {
            using AppDbContext dbContext = await dbContextFactory.CreateDbContextAsync();
            return await dbContext.ScenarioNpcs
                .Include(n => n.Character)
                .FirstOrDefaultAsync(n => n.Id == id);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving NPC with ID {NpcId}", id);
            return null;
        }
    }
    
    /// <summary>
    /// Creates a new NPC in a scenario
    /// </summary>
    /// <param name="npc">The NPC to create</param>
    /// <returns>The created NPC with its assigned ID</returns>
    public async Task<ScenarioNpc?> CreateNpcAsync(ScenarioNpc npc)
    {
        try
        {
            using AppDbContext dbContext = await dbContextFactory.CreateDbContextAsync();
            
            // Verify that the scenario exists
            bool scenarioExists = await dbContext.Scenarios.AnyAsync(s => s.Id == npc.ScenarioId);
            if (!scenarioExists)
            {
                logger.LogWarning("Cannot create NPC: scenario {ScenarioId} does not exist", npc.ScenarioId);
                return null;
            }
            
            await dbContext.ScenarioNpcs.AddAsync(npc);
            await dbContext.SaveChangesAsync();
            
            return npc;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating NPC {NpcName} for scenario {ScenarioId}", 
                npc.Name, npc.ScenarioId);
            return null;
        }
    }
    
    /// <summary>
    /// Updates an existing NPC
    /// </summary>
    /// <param name="npc">The NPC with updated values</param>
    /// <returns>True if the update was successful, false otherwise</returns>
    public async Task<bool> UpdateNpcAsync(ScenarioNpc npc)
    {
        try
        {
            using AppDbContext dbContext = await dbContextFactory.CreateDbContextAsync();
            
            ScenarioNpc? existingNpc = await dbContext.ScenarioNpcs.FindAsync(npc.Id);
            if (existingNpc is null)
            {
                return false;
            }
            
            // Update the existing NPC with the new values
            dbContext.Entry(existingNpc).CurrentValues.SetValues(npc);
            await dbContext.SaveChangesAsync();
            
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error updating NPC {NpcId}", npc.Id);
            return false;
        }
    }
    
    /// <summary>
    /// Deletes an NPC by its ID
    /// </summary>
    /// <param name="id">The ID of the NPC to delete</param>
    /// <returns>True if the deletion was successful, false otherwise</returns>
    public async Task<bool> DeleteNpcAsync(Guid id)
    {
        try
        {
            using AppDbContext dbContext = await dbContextFactory.CreateDbContextAsync();
            
            ScenarioNpc? npc = await dbContext.ScenarioNpcs.FindAsync(id);
            if (npc is null)
            {
                return false;
            }
            
            dbContext.ScenarioNpcs.Remove(npc);
            await dbContext.SaveChangesAsync();
            
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deleting NPC {NpcId}", id);
            return false;
        }
    }
    
    /// <summary>
    /// Links an NPC to an existing character
    /// </summary>
    /// <param name="npcId">The ID of the NPC</param>
    /// <param name="characterId">The ID of the character to link</param>
    /// <returns>True if the linking was successful, false otherwise</returns>
    public async Task<bool> LinkNpcToCharacterAsync(Guid npcId, Guid characterId)
    {
        try
        {
            using AppDbContext dbContext = await dbContextFactory.CreateDbContextAsync();
            
            ScenarioNpc? npc = await dbContext.ScenarioNpcs.FindAsync(npcId);
            if (npc is null)
            {
                return false;
            }
            
            // Verify that the character exists
            bool characterExists = await dbContext.CharacterStorage.AnyAsync(c => c.Id == characterId);
            if (!characterExists)
            {
                logger.LogWarning("Cannot link NPC to character: character {CharacterId} does not exist", characterId);
                return false;
            }
            
            npc.CharacterId = characterId;
            await dbContext.SaveChangesAsync();
            
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error linking NPC {NpcId} to character {CharacterId}", npcId, characterId);
            return false;
        }
    }
    
    /// <summary>
    /// Unlinks an NPC from its associated character
    /// </summary>
    /// <param name="npcId">The ID of the NPC</param>
    /// <returns>True if the unlinking was successful, false otherwise</returns>
    public async Task<bool> UnlinkNpcFromCharacterAsync(Guid npcId)
    {
        try
        {
            using AppDbContext dbContext = await dbContextFactory.CreateDbContextAsync();
            
            ScenarioNpc? npc = await dbContext.ScenarioNpcs.FindAsync(npcId);
            if (npc is null)
            {
                return false;
            }
            
            npc.CharacterId = null;
            await dbContext.SaveChangesAsync();
            
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error unlinking NPC {NpcId} from character", npcId);
            return false;
        }
    }
}
