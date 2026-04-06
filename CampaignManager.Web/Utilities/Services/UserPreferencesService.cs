using CampaignManager.Web.Model;
using CampaignManager.Web.Utilities.DataBase;
using Microsoft.EntityFrameworkCore;

namespace CampaignManager.Web.Utilities.Services;

public sealed class UserPreferencesService(
    IDbContextFactory<AppDbContext> dbContextFactory,
    IdentityService identityService,
    ILogger<UserPreferencesService> logger)
{
    public async Task<string?> GetAsync(string key)
    {
        var email = identityService.GetCurrentUserEmail();
        if (email is null) return null;

        try
        {
            await using var db = await dbContextFactory.CreateDbContextAsync();
            var prefs = await db.UserPreferences.FirstOrDefaultAsync(p => p.UserEmail == email);
            return prefs?.Preferences.GetValueOrDefault(key);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error reading preference {Key} for {Email}", key, email);
            return null;
        }
    }

    public async Task SetAsync(string key, string value)
    {
        var email = identityService.GetCurrentUserEmail();
        if (email is null) return;

        try
        {
            await using var db = await dbContextFactory.CreateDbContextAsync();
            var prefs = await db.UserPreferences.FirstOrDefaultAsync(p => p.UserEmail == email);
            if (prefs is null)
            {
                prefs = new UserPreferences { UserEmail = email };
                prefs.Init();
                db.UserPreferences.Add(prefs);
            }

            prefs.Preferences[key] = value;
            prefs.LastUpdated = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error saving preference {Key} for {Email}", key, email);
        }
    }
}
