using CampaignManager.Web.Components.Features.Skills.Model;
using CampaignManager.Web.Utilities.DataBase;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace CampaignManager.Web.Components.Features.Skills.Services;

/// <summary>
/// Service for managing skills in the system
/// </summary>
public sealed class SkillService(
    IDbContextFactory<AppDbContext> dbContextFactory,
    IMemoryCache cache,
    ILogger<SkillService> logger)
{
    private const string SkillsCacheKey = "AllSkills";
    private const int DefaultPageSize = 6;
    private static readonly TimeSpan CacheExpiration = TimeSpan.FromMinutes(15);

    /// <summary>
    /// Gets all skills in the system
    /// </summary>
    /// <param name="searchText">Optional search text to filter skills</param>
    /// <param name="skillCategoryStr">Optional skill category to filter by</param>
    /// <param name="page">Optional page number for pagination</param>
    /// <param name="pageSize">Optional page size for pagination</param>
    /// <returns>A list of all skills</returns>
    public async Task<List<SkillModel>> GetAllSkillsAsync(string searchText = "", string? skillCategoryStr = null, int page = 1, int pageSize = DefaultPageSize)
    {
        try
        {
            if (cache.TryGetValue(SkillsCacheKey, out List<SkillModel>? skills) && skills is not null)
                return FilterAndPage(skills);

            await using var dbContext = await dbContextFactory.CreateDbContextAsync();
            skills = await dbContext.Skills
                .OrderBy(s => s.Name)
                .ToListAsync();

            var cacheOptions = new MemoryCacheEntryOptions()
                .SetAbsoluteExpiration(CacheExpiration);

            cache.Set(SkillsCacheKey, skills, cacheOptions);

            return FilterAndPage(skills);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving skills");
            return [];
        }

        List<SkillModel> FilterAndPage(List<SkillModel> skills)
        {
            var isCategoryFilter = Enum.TryParse(skillCategoryStr, out SkillCategory skillCategory);
            return skills
                .Where(s => string.IsNullOrWhiteSpace(searchText) || s.Name.ToLower().Contains(searchText.ToLower()))
                .Where(s => isCategoryFilter == false || s.Category == skillCategory)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();
        }
    }

    /// <summary>
    /// Gets the total count of skills
    /// </summary>
    /// <param name="searchText">Optional search text to filter skills</param>
    /// <param name="skillCategoryStr">Optional skill category to filter by</param>
    /// <returns>The total count of skills</returns>
    public async Task<int> GetSkillCountAsync(string searchText = "", string? skillCategoryStr = null)
    {
        try
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync();
            var isCategoryFilter = Enum.TryParse(skillCategoryStr, out SkillCategory skillCategory);
            return await dbContext.Skills
                .Where(s => string.IsNullOrWhiteSpace(searchText) || s.Name.ToLower().Contains(searchText.ToLower()))
                .Where(s => isCategoryFilter == false || s.Category == skillCategory)
                .CountAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving skill count");
            return 0;
        }
    }

    /// <summary>
    /// Gets a skill by its ID
    /// </summary>
    /// <param name="id">The ID of the skill</param>
    /// <returns>The skill if found, null otherwise</returns>
    public async Task<SkillModel?> GetSkillByIdAsync(Guid id)
    {
        try
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync();
            return await dbContext.Skills.FindAsync(id);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving skill with ID {SkillId}", id);
            return null;
        }
    }

    /// <summary>
    /// Creates a new skill
    /// </summary>
    /// <param name="skill">The skill to create</param>
    /// <returns>The created skill with its assigned ID</returns>
    public async Task<SkillModel?> CreateSkillAsync(SkillModel skill)
    {
        try
        {
            using var dbContext = await dbContextFactory.CreateDbContextAsync();

            // Check if a skill with the same name already exists
            var exists = await dbContext.Skills.AnyAsync(s => s.Name == skill.Name);
            if (exists)
            {
                logger.LogWarning("Skill with name {SkillName} already exists", skill.Name);
                return null;
            }

            await dbContext.Skills.AddAsync(skill);
            await dbContext.SaveChangesAsync();

            // Invalidate cache
            cache.Remove(SkillsCacheKey);

            return skill;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating skill {SkillName}", skill.Name);
            return null;
        }
    }

    /// <summary>
    /// Updates an existing skill
    /// </summary>
    /// <param name="skill">The skill with updated values</param>
    /// <returns>True if the update was successful, false otherwise</returns>
    public async Task<bool> UpdateSkillAsync(SkillModel skill)
    {
        try
        {
            using var dbContext = await dbContextFactory.CreateDbContextAsync();

            var existingSkill = await dbContext.Skills.FindAsync(skill.Id);
            if (existingSkill is null)
            {
                logger.LogWarning("Skill with ID {SkillId} not found for update", skill.Id);
                return false;
            }

            // Check if the name is being changed and if the new name already exists
            if (existingSkill.Name != skill.Name)
            {
                var nameExists = await dbContext.Skills
                    .AnyAsync(s => s.Name == skill.Name && s.Id != skill.Id);

                if (nameExists)
                {
                    logger.LogWarning("Cannot update skill {SkillId}: another skill with name {SkillName} already exists",
                        skill.Id, skill.Name);
                    return false;
                }
            }

            dbContext.Entry(existingSkill).CurrentValues.SetValues(skill);
            await dbContext.SaveChangesAsync();

            // Invalidate cache
            cache.Remove(SkillsCacheKey);

            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error updating skill {SkillId}", skill.Id);
            return false;
        }
    }

    /// <summary>
    /// Deletes a skill by its ID
    /// </summary>
    /// <param name="id">The ID of the skill to delete</param>
    /// <returns>True if the deletion was successful, false otherwise</returns>
    public async Task<bool> DeleteSkillAsync(Guid id)
    {
        try
        {
            using var dbContext = await dbContextFactory.CreateDbContextAsync();

            var skill = await dbContext.Skills.FindAsync(id);
            if (skill is null) return false;

            dbContext.Skills.Remove(skill);
            await dbContext.SaveChangesAsync();

            // Invalidate cache
            cache.Remove(SkillsCacheKey);

            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deleting skill {SkillId}", id);
            return false;
        }
    }
}