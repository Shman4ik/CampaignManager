using CampaignManager.Web.Components.Features.Admin.Model;
using CampaignManager.Web.Components.Features.Characters.Model;
using CampaignManager.Web.Model;
using CampaignManager.Web.Utilities.DataBase;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace CampaignManager.Web.Utilities.Services;

public sealed class IdentityService(
    IHttpContextAccessor httpContextAccessor,
    AuthenticationStateProvider authenticationStateProvider,
    IDbContextFactory<AppIdentityDbContext> appIdentityDbContextFactory,
    IDbContextFactory<AppDbContext> appDbContextFactory,
    ILogger<IdentityService> logger)
{
    /// <summary>
    ///     Строка <c>AspNetUsers</c> текущего пользователя на время области — HTTP-запроса пререндера
    ///     или circuit. Запоминается <b>задача</b>, а не результат: острова одной страницы стартуют
    ///     одновременно, и пока первый ждёт базу, остальные при кэше «по результату» уходили бы за
    ///     той же строкой сами — так главная и делала по три одинаковых SELECT на проход.
    ///     <para>
    ///         Роль и имя отсюда не берутся: для прав есть claims (их кормит <c>UserClaimsCache</c>,
    ///         который сбрасывают при смене роли и имени), а эта строка нужна только тем, кому важно,
    ///         есть ли пользователь в базе, или нужны поля, которых в claims нет.
    ///     </para>
    /// </summary>
    private Task<ApplicationUser?>? _currentUser;

    /// <summary>
    /// Sync — works during SSR/prerender (HttpContext available).
    /// Returns null during interactive WebSocket rendering.
    /// Prefer <see cref="GetCurrentUserEmailAsync"/> in interactive components.
    /// </summary>
    public string? GetCurrentUserEmail()
    {
        return httpContextAccessor.HttpContext?.User.FindFirst(ClaimTypes.Email)?.Value;
    }

    /// <summary>
    /// Async — works in both SSR prerender and interactive Blazor Server rendering.
    /// Falls back to AuthenticationStateProvider when HttpContext is unavailable.
    /// </summary>
    public async Task<string?> GetCurrentUserEmailAsync()
    {
        var email = GetCurrentUserEmail();
        if (email is not null) return email;

        var state = await authenticationStateProvider.GetAuthenticationStateAsync();
        return state.User.FindFirst(ClaimTypes.Email)?.Value;
    }

    public async Task<bool> IsKeeper()
    {
        return await GetCurrentUserRole() is PlayerRole.GameMaster or PlayerRole.Administrator;
    }

    public async Task<bool> IsAdministrator()
    {
        return await GetCurrentUserRole() is PlayerRole.Administrator;
    }

    /// <summary>
    ///     Серверная граница для правки общего контента — справочников оружия, предметов, заклинаний,
    ///     книг, бестиария, навыков, профессий и фонотеки: Хранитель и администратор проходят, остальным летит
    ///     <see cref="UnauthorizedAccessException" />. Спрятанная в интерфейсе кнопка защитой не
    ///     считается: метод сервиса можно вызвать и в обход неё.
    /// </summary>
    public async Task EnsureKeeperAsync(string operation)
    {
        if (await IsKeeper())
            return;

        var email = await GetCurrentUserEmailAsync();
        logger.LogWarning("Denied keeper operation {Operation} for {Email}", operation, email ?? "<anonymous>");
        throw new UnauthorizedAccessException($"Операция «{operation}» доступна только Хранителю");
    }

    public async Task<PlayerRole> GetCurrentUserRole()
    {
        // RoleClaimsTransformation already adds ClaimTypes.Role to the principal on every request.
        // Read it from there to avoid an extra round-trip to the database.
        var principal = await GetCurrentPrincipalAsync();
        var roleStr = principal?.FindFirst(ClaimTypes.Role)?.Value;
        if (roleStr is not null && Enum.TryParse<PlayerRole>(roleStr, out var roleFromClaims))
            return roleFromClaims;

        // Fallback: user not yet authenticated or claims not yet transformed.
        var user = await GetUserAsync();
        return user?.Role ?? PlayerRole.Player;
    }

    private async Task<ClaimsPrincipal?> GetCurrentPrincipalAsync()
    {
        var httpUser = httpContextAccessor.HttpContext?.User;
        if (httpUser?.Identity?.IsAuthenticated == true)
            return httpUser;

        var state = await authenticationStateProvider.GetAuthenticationStateAsync();
        return state.User.Identity?.IsAuthenticated == true ? state.User : null;
    }

    public async Task<bool> HasPendingKeeperApplicationAsync()
    {
        var email = await GetCurrentUserEmailAsync();
        if (email is null) return false;

        await using var db = await appDbContextFactory.CreateDbContextAsync();
        return await db.KeeperApplications
            .AnyAsync(a => a.UserEmail.ToLower() == email.ToLower()
                           && a.Status == KeeperApplicationStatus.Pending);
    }

    public async Task<ApplicationUser?> GetUserAsync()
    {
        var load = _currentUser ??= LoadCurrentUserAsync();
        try
        {
            return await load;
        }
        catch
        {
            // Упавшую загрузку не запоминаем, иначе circuit до конца жизни получал бы ту же ошибку.
            if (ReferenceEquals(_currentUser, load))
                _currentUser = null;
            throw;
        }
    }

    public async Task<ApplicationUser?> GetUserAsync(string? email)
    {
        if (email == null) return null;

        var currentEmail = await GetCurrentUserEmailAsync();
        if (string.Equals(currentEmail, email, StringComparison.OrdinalIgnoreCase))
            return await GetUserAsync();

        return await GetUserByEmailFromDb(email);
    }

    private async Task<ApplicationUser?> LoadCurrentUserAsync() =>
        await GetUserByEmailFromDb(await GetCurrentUserEmailAsync());

    private async Task<ApplicationUser?> GetUserByEmailFromDb(string? email)
    {
        if (email == null) return null;

        await using var dbContext = await appIdentityDbContextFactory.CreateDbContextAsync();
        return await dbContext.Users
            .AsNoTracking()
            .SingleOrDefaultAsync(p => p.Email != null && p.Email.ToLower() == email.ToLower());
    }

    public async Task<ApplicationUser?> CreateUserAsync(ApplicationUser user)
    {
        await using var dbContext = await appIdentityDbContextFactory.CreateDbContextAsync();
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        // Запомненное «пользователя нет» после создания строки стало бы неправдой.
        _currentUser = null;
        return user;
    }
}
