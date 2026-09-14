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
        var email = await identityService.GetCurrentUserEmailAsync();
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

    /// <summary>
    ///     Все настройки одним запросом — для компонентов, которым нужно сразу несколько ключей.
    /// </summary>
    public async Task<IReadOnlyDictionary<string, string>> GetAllAsync()
    {
        var email = await identityService.GetCurrentUserEmailAsync();
        if (email is null) return new Dictionary<string, string>();

        try
        {
            await using var db = await dbContextFactory.CreateDbContextAsync();
            var prefs = await db.UserPreferences.FirstOrDefaultAsync(p => p.UserEmail == email);
            return prefs?.Preferences ?? new Dictionary<string, string>();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error reading preferences for {Email}", email);
            return new Dictionary<string, string>();
        }
    }

    public async Task<bool> GetBoolAsync(string key, bool defaultValue)
    {
        var raw = await GetAsync(key);
        return bool.TryParse(raw, out var value) ? value : defaultValue;
    }

    public Task SetBoolAsync(string key, bool value) =>
        SetAsync(key, value ? "true" : "false");

    public Task SetAsync(string key, string value) =>
        SetManyAsync(new Dictionary<string, string> { [key] = value });

    /// <summary>
    ///     Пишет несколько ключей за одно сохранение — иначе каждая настройка стоит отдельного
    ///     чтения и апдейта одной и той же JSONB-строки.
    /// </summary>
    public async Task SetManyAsync(IReadOnlyDictionary<string, string> values)
    {
        if (values.Count == 0) return;

        var email = await identityService.GetCurrentUserEmailAsync();
        if (email is null) return;

        try
        {
            await using var db = await dbContextFactory.CreateDbContextAsync();
            var prefs = await db.UserPreferences.FirstOrDefaultAsync(p => p.UserEmail == email);
            var isNew = prefs is null;
            if (prefs is null)
            {
                prefs = new UserPreferences { UserEmail = email };
                prefs.Init();
                db.UserPreferences.Add(prefs);
            }

            foreach (var (key, value) in values)
                prefs.Preferences[key] = value;

            prefs.LastUpdated = DateTimeOffset.UtcNow;

            // Словарь меняется на месте, ссылка та же — для JSONB-колонки без компаратора это
            // означает, что трекер изменений ничего не заметит и UPDATE уйдёт с одной датой.
            // Первая запись ключа при этом проходит (INSERT новой строки), а вторая молча теряется.
            if (!isNew)
                db.Entry(prefs).Property(p => p.Preferences).IsModified = true;

            await db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error saving preferences {Keys} for {Email}", string.Join(", ", values.Keys), email);
        }
    }
}
