using System.Security.Claims;
using CampaignManager.Core.Identity;
using CampaignManager.Data;
using CampaignManager.Data.Identity;
using CampaignManager.Server.Access;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CampaignManager.Server.Identity;

/// <summary>
/// Белый список и первые администраторы — секция <c>Authorization</c>, как в v1. Пустые
/// <see cref="AllowedEmails"/> и <see cref="AllowedDomains"/> — пускать всех с подтверждённой почтой.
/// </summary>
public sealed class AccessListOptions
{
    public const string Section = "Authorization";

    public string[] AllowedEmails { get; set; } = [];
    public string[] AllowedDomains { get; set; } = [];

    /// <summary>Кто становится администратором при входе (первый админ появляется отсюда).</summary>
    public string[] AdminEmails { get; set; } = [];
}

/// <summary>Claims сессии 2.0. Остальные — те же, что у кук v1: <c>ClaimTypes.Email/Name/NameIdentifier</c>.</summary>
public static class CmClaims
{
    /// <summary>Id строки <c>cm.users</c>. У кук, выданных v1, его нет — там поиск по sub и почте.</summary>
    public const string UserId = "cm_uid";
}

/// <summary>Кто вошёл через Auth0: sub, подтверждённая почта и имя из токена.</summary>
public sealed record ExternalLogin(string Subject, string Email, string? Name);

/// <summary>
/// Пользователи платформы: заводятся при входе, находятся по сессии. Поиск при входе — по
/// <c>auth0_sub</c>, затем по почте: так перенесённый из v1 человек (sub пуст) получает свою строку,
/// а не новую.
/// </summary>
public sealed class UserDirectory(
    CmDbContext dbContext,
    IOptions<AccessListOptions> options,
    TimeProvider timeProvider,
    ILogger<UserDirectory> logger)
{
    public bool IsAllowed(string email)
    {
        var list = options.Value;
        if (list.AllowedEmails.Length == 0 && list.AllowedDomains.Length == 0)
        {
            return true;
        }

        var domain = email[(email.LastIndexOf('@') + 1)..];
        return list.AllowedEmails.Contains(email, StringComparer.OrdinalIgnoreCase)
               || list.AllowedDomains.Contains(domain, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Вход: найти или завести пользователя, привязать sub, поднять до админа по <c>AdminEmails</c>.
    /// Почта уже проверена вызывающим (<c>email_verified</c>): на ней держится связь с перенесёнными
    /// данными, и неподтверждённый адрес унаследовал бы чужие кампании. Не в белом списке — <c>null</c>.
    /// </summary>
    public async Task<User?> SignInAsync(ExternalLogin login, CancellationToken cancellationToken = default)
    {
        if (!IsAllowed(login.Email))
        {
            logger.LogWarning("Вход {Email} отклонён: адреса нет в белом списке", login.Email);
            return null;
        }

        // email — citext: сравнение в базе без учёта регистра, ToLower() не нужен.
        var user = await dbContext.Users.SingleOrDefaultAsync(u => u.Auth0Sub == login.Subject, cancellationToken)
                   ?? await dbContext.Users.SingleOrDefaultAsync(u => u.Email == login.Email, cancellationToken);

        if (user is null)
        {
            user = new User
            {
                Auth0Sub = login.Subject,
                Email = login.Email,
                DisplayName = string.IsNullOrWhiteSpace(login.Name) ? login.Email : login.Name.Trim(),
            };
            dbContext.Users.Add(user);
            logger.LogInformation("Заведён пользователь {Email}", login.Email);
        }
        else if (user.Auth0Sub is null)
        {
            user.Auth0Sub = login.Subject;
            logger.LogInformation("Пользователь {Email} привязан к учётке Auth0", login.Email);
        }
        else if (user.Auth0Sub != login.Subject)
        {
            // Тот же человек вошёл другим способом (Google и пароль — разные sub). Почта подтверждена,
            // поэтому это он; привязка остаётся за первым способом, а сессия держится на id.
            logger.LogInformation("Пользователь {Email} вошёл другим способом входа", login.Email);
        }

        if (options.Value.AdminEmails.Contains(login.Email, StringComparer.OrdinalIgnoreCase)
            && user.Role is not UserRole.Admin)
        {
            user.Role = UserRole.Admin;
            logger.LogInformation("Пользователь {Email} назначен администратором из AdminEmails", login.Email);
        }

        user.LastLoginAt = timeProvider.GetUtcNow();
        await dbContext.SaveChangesAsync(cancellationToken);
        return user;
    }

    /// <summary>
    /// Пользователь сессии: по <see cref="CmClaims.UserId"/> (куки 2.0), иначе по sub и почте (куки v1
    /// и токены приложения). Если строки нет или она ещё не привязана к Auth0 (перенесённая), а в сессии
    /// есть sub и подтверждённая почта — это первый запрос приложения с JWT: он и есть вход.
    /// </summary>
    public async Task<SignedInUser?> ResolveAsync(ClaimsPrincipal principal, CancellationToken cancellationToken = default)
    {
        if (Guid.TryParse(principal.FindFirstValue(CmClaims.UserId), out var userId))
        {
            return (await Find(u => u.Id == userId))?.User;
        }

        var subject = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        var email = principal.FindFirstValue(ClaimTypes.Email);

        var found = (subject is null ? null : await Find(u => u.Auth0Sub == subject))
                    ?? (email is null ? null : await Find(u => u.Email == email));
        if (found is { Linked: true } || subject is null || email is null)
        {
            return found?.User;
        }

        var signedIn = await SignInAsync(new ExternalLogin(subject, email, principal.FindFirstValue(ClaimTypes.Name)), cancellationToken);
        return signedIn is null ? null : new SignedInUser(signedIn.Id, signedIn.Email, signedIn.DisplayName, signedIn.Role);

        async Task<(SignedInUser User, bool Linked)?> Find(System.Linq.Expressions.Expression<Func<User, bool>> predicate)
        {
            var row = await dbContext.Users.Where(predicate)
                .Select(u => new { User = new SignedInUser(u.Id, u.Email, u.DisplayName, u.Role), Linked = u.Auth0Sub != null })
                .SingleOrDefaultAsync(cancellationToken);
            return row is null ? null : (row.User, row.Linked);
        }
    }
}
