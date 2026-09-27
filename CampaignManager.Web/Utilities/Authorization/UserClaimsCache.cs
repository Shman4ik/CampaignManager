using CampaignManager.Web.Components.Features.Characters.Model;
using CampaignManager.Web.Utilities.DataBase;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace CampaignManager.Web.Utilities.Authorization;

/// <summary>
///     Роль и отображаемое имя пользователя для <see cref="RoleClaimsTransformation" />,
///     закэшированные по нормализованной почте.
///     <para>
///         <b>Не убирать.</b> <c>UseAuthentication</c> стоит до <c>MapStaticAssets</c>, поэтому
///         трансформация claims отрабатывает на каждый HTTP-запрос — на каждый CSS, JS, шрифт и
///         картинку из <c>/api/minio</c>, а не только на страницу. Без кэша одна загрузка страницы
///         давала 13–16 одинаковых SELECT в <c>identity."AspNetUsers"</c> — 64 % всех запросов к базе
///         в логе. Порядок middleware не трогаем: картинкам из <c>/api/minio</c> нужна авторизация.
///     </para>
///     <para>
///         Кто меняет роль или имя, обязан позвать <see cref="Invalidate" /> — иначе новое значение
///         доедет до claims только через <see cref="Lifetime" />. Сейчас это
///         <c>AdminService.SetUserRoleAsync</c>, <c>ProfileService.UpdateDisplayNameAsync</c> и
///         вход через Google в <c>Program.cs</c> (там роль поднимается до администратора).
///     </para>
/// </summary>
public sealed class UserClaimsCache(
    IMemoryCache cache,
    IDbContextFactory<AppIdentityDbContext> identityDbContextFactory)
{
    /// <summary>
    ///     Сколько живёт запись. Страховка на случай пропущенного <see cref="Invalidate" />: даже
    ///     тогда роль и имя разойдутся с базой не дольше, чем на столько.
    /// </summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(5);

    /// <summary>
    ///     Промахи ждут друг друга: при холодном кэше страница запрашивает десяток файлов разом,
    ///     и без этого каждый из них сходил бы в базу за одной и той же строкой.
    /// </summary>
    private static readonly SemaphoreSlim LoadGate = new(1, 1);

    public sealed record Entry(PlayerRole Role, string? DisplayName);

    /// <summary>
    ///     Роль и имя из <c>AspNetUsers</c>; <c>null</c>, если такого пользователя нет. Отсутствие
    ///     не кэшируется: строка может появиться следующим же запросом (вход, вступление в кампанию).
    /// </summary>
    public async Task<Entry?> GetAsync(string email)
    {
        var key = Key(email);
        if (cache.TryGetValue(key, out Entry? cached) && cached is not null)
            return cached;

        await LoadGate.WaitAsync();
        try
        {
            if (cache.TryGetValue(key, out cached) && cached is not null)
                return cached;

            await using var db = await identityDbContextFactory.CreateDbContextAsync();
            var entry = await db.Users
                .AsNoTracking()
                .Where(u => u.Email != null && u.Email.ToLower() == email.ToLower())
                .Select(u => new Entry(u.Role, u.UserName))
                .SingleOrDefaultAsync();

            if (entry is not null)
                cache.Set(key, entry, Lifetime);

            return entry;
        }
        finally
        {
            LoadGate.Release();
        }
    }

    /// <summary>Сбросить запись: следующий запрос этого пользователя перечитает роль и имя из базы.</summary>
    public void Invalidate(string? email)
    {
        if (!string.IsNullOrWhiteSpace(email))
            cache.Remove(Key(email));
    }

    /// <summary>Ключ один на всех — строка ключа не должна дублироваться у тех, кто сбрасывает.</summary>
    private static string Key(string email) => $"user-claims:{email.Trim().ToUpperInvariant()}";
}
