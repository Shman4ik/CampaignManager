using CampaignManager.Web.Components.Features.Skills.Model;
using CampaignManager.Web.Utilities.DataBase;
using CampaignManager.Web.Utilities.Services;
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

    /// <summary>
    /// Gets all skills in the system
    /// </summary>
    public async Task<List<SkillModel>> GetAllSkillsAsync(string searchText = "", string? skillCategoryStr = null, int page = 1, int pageSize = DefaultPageSize)
    {
        var all = await CrudServiceHelper.GetAllCachedAsync<SkillModel>(dbContextFactory, cache, SkillsCacheKey, logger);
        return FilterAndPage(all);

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
    public Task<SkillModel?> GetSkillByIdAsync(Guid id) =>
        CrudServiceHelper.GetByIdAsync<SkillModel>(dbContextFactory, id, logger);

    /// <summary>
    /// Creates a new skill, rejecting duplicates by name
    /// </summary>
    public Task<SkillModel?> CreateSkillAsync(SkillModel skill) =>
        CrudServiceHelper.CreateAsync(dbContextFactory, cache, SkillsCacheKey, skill, logger);

    /// <summary>
    /// Updates an existing skill
    /// </summary>
    public Task<bool> UpdateSkillAsync(SkillModel skill) =>
        CrudServiceHelper.UpdateAsync(dbContextFactory, cache, SkillsCacheKey, skill, logger);

    /// <summary>
    /// Deletes a skill by its ID
    /// </summary>
    public Task<bool> DeleteSkillAsync(Guid id) =>
        CrudServiceHelper.DeleteAsync<SkillModel>(dbContextFactory, cache, SkillsCacheKey, id, logger);
}