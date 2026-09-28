using CampaignManager.Web.Model;
using CampaignManager.Web.Utilities.DataBase;
using Microsoft.EntityFrameworkCore;

namespace CampaignManager.Web.Utilities.Services;

public sealed class UserPreferencesService(
    IDbContextFactory<AppDbContext> dbContextFactory,
    IdentityService identityService,
    ILogger<UserPreferencesService> logger)
{
    /// <summary>
    ///     Сколько живёт прочитанный словарь. Настройки на одной загрузке страницы читают сразу
    ///     несколько независимых компонентов — боковое меню, нижняя навигация, плеер, сама
    ///     страница, — и каждый раньше отдельно тянул всю JSONB-строку. Сервис scoped, то есть
    ///     кэш живёт в пределах одного circuit (или одного HTTP-запроса пререндера) и чужому
    ///     пользователю не достанется. Срок короткий, чтобы правка с другого устройства доехала
    ///     до открытой вкладки без перезагрузки; своя запись обновляет кэш сразу.
    /// </summary>
    private static readonly TimeSpan CacheLifetime = TimeSpan.FromSeconds(30);

    // Кэшируется задача, а не готовый словарь: компоненты лэйаута стартуют почти одновременно,
    // и второй должен дождаться уже идущего запроса, а не отправить свой.
    private Task<Dictionary<string, string>?>? _cached;
    private string? _cachedFor;
    private DateTimeOffset _cachedAt;

    public async Task<string?> GetAsync(string key)
    {
        var prefs = await LoadAsync();
        return prefs?.GetValueOrDefault(key);
    }

    /// <summary>
    ///     Все настройки одним запросом — для компонентов, которым нужно сразу несколько ключей.
    ///     Возвращает копию: закэшированный словарь вызывающему менять нельзя.
    /// </summary>
    public async Task<IReadOnlyDictionary<string, string>> GetAllAsync()
    {
        var prefs = await LoadAsync();
        return prefs is null ? new Dictionary<string, string>() : new Dictionary<string, string>(prefs);
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
            // Чтение здесь — из базы, мимо кэша: пишем поверх того, что лежит в строке сейчас,
            // иначе запись с этой вкладки затёрла бы ключи, сохранённые с другой.
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

            Remember(email, new Dictionary<string, string>(prefs.Preferences));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error saving preferences {Keys} for {Email}", string.Join(", ", values.Keys), email);
        }
    }

    private async Task<Dictionary<string, string>?> LoadAsync()
    {
        var email = await identityService.GetCurrentUserEmailAsync();
        if (email is null) return null;

        if (_cached is null
            || _cachedFor != email
            || DateTimeOffset.UtcNow - _cachedAt > CacheLifetime)
        {
            _cachedFor = email;
            _cachedAt = DateTimeOffset.UtcNow;
            _cached = ReadAsync(email);
        }

        var loading = _cached;
        var prefs = await loading;

        // Ошибку чтения не кэшируем: следующий вызов пусть попробует ещё раз.
        if (prefs is null && ReferenceEquals(_cached, loading))
            _cached = null;

        return prefs;
    }

    private void Remember(string email, Dictionary<string, string> prefs)
    {
        _cachedFor = email;
        _cachedAt = DateTimeOffset.UtcNow;
        _cached = Task.FromResult<Dictionary<string, string>?>(prefs);
    }

    /// <returns>Словарь настроек (пустой, если строки ещё нет) или <c>null</c> при ошибке чтения.</returns>
    private async Task<Dictionary<string, string>?> ReadAsync(string email)
    {
        try
        {
            await using var db = await dbContextFactory.CreateDbContextAsync();
            var prefs = await db.UserPreferences
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.UserEmail == email);
            return prefs is null ? [] : new Dictionary<string, string>(prefs.Preferences);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error reading preferences for {Email}", email);
            return null;
        }
    }
}
