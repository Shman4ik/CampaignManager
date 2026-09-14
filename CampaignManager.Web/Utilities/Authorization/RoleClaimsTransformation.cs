using System.Security.Claims;
using CampaignManager.Web.Model;
using CampaignManager.Web.Utilities.DataBase;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication;

namespace CampaignManager.Web.Utilities.Authorization;

/// <summary>
///     Подмешивает в принципал то, чем владеет приложение, а не Google: роль и отображаемое имя.
///     Оба значения живут в <see cref="ApplicationUser" />, а кука после входа знает только то,
///     что отдал Google, — см. Features/Profile/CLAUDE.md.
/// </summary>
public sealed class RoleClaimsTransformation(
    IDbContextFactory<AppIdentityDbContext> identityDbContextFactory) : IClaimsTransformation
{
    public async Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        if (principal.Identity?.IsAuthenticated != true)
            return principal;

        var email = principal.FindFirst(ClaimTypes.Email)?.Value;
        if (email is null)
            return principal;

        // Роль добавляется здесь же, поэтому её наличие означает, что мы на этом запросе уже
        // отработали: TransformAsync вызывается по нескольку раз за запрос.
        if (principal.HasClaim(c => c.Type == ClaimTypes.Role))
            return principal;

        await using var db = await identityDbContextFactory.CreateDbContextAsync();
        var user = await db.Users.SingleOrDefaultAsync(u => u.Email != null && u.Email.ToLower() == email.ToLower());

        if (user is null)
            return principal;

        if (principal.Identity is not ClaimsIdentity identity)
            return principal;

        identity.AddClaim(new Claim(ClaimTypes.Role, user.Role.ToString()));
        ApplyDisplayName(identity, user.UserName);

        return principal;
    }

    /// <summary>
    ///     Имя из базы важнее имени из Google: первое пользователь меняет в личном кабинете,
    ///     второе застыло на момент входа. Claim именно заменяется, а не добавляется вторым —
    ///     <see cref="ClaimsIdentity.Name" /> читает первый claim своего типа, так что
    ///     добавленный следом не увидел бы никто.
    /// </summary>
    private static void ApplyDisplayName(ClaimsIdentity identity, string? displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName)) return;
        if (string.Equals(identity.Name, displayName, StringComparison.Ordinal)) return;

        var replaced = true;
        foreach (var stale in identity.FindAll(identity.NameClaimType).ToList())
            replaced &= identity.TryRemoveClaim(stale);

        // Не удалось снять старый claim — добавлять свой бессмысленно, только плодить дубли.
        if (replaced)
            identity.AddClaim(new Claim(identity.NameClaimType, displayName));
    }
}
