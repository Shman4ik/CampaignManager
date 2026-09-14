using CampaignManager.Web.Components.Features.Characters.Model;
using CampaignManager.Web.Components.Features.Profile.Model;
using CampaignManager.Web.Utilities;
using CampaignManager.Web.Utilities.DataBase;
using CampaignManager.Web.Utilities.Services;
using Microsoft.EntityFrameworkCore;

namespace CampaignManager.Web.Components.Features.Profile.Services;

/// <summary>
///     Личный кабинет: чтение сводки о пользователе и смена отображаемого имени.
///     Открывает оба контекста — сам пользователь лежит в схеме identity, а его кампании
///     и листы в games.
/// </summary>
public sealed class ProfileService(
    IDbContextFactory<AppIdentityDbContext> identityDbContextFactory,
    IDbContextFactory<AppDbContext> dbContextFactory,
    IdentityService identityService,
    ILogger<ProfileService> logger)
{
    public const int MaxDisplayNameLength = 64;

    public async Task<UserProfile?> GetProfileAsync()
    {
        var email = await identityService.GetCurrentUserEmailAsync();
        if (email is null) return null;

        var user = await identityService.GetUserAsync(email);
        if (user is null) return null;

        var emailLower = email.ToLower();
        await using var db = await dbContextFactory.CreateDbContextAsync();

        var playerIds = await db.CampaignPlayers
            .Where(p => p.PlayerEmail.ToLower() == emailLower)
            .Select(p => p.Id)
            .ToListAsync();

        var campaignCount = await db.Campaigns
            .CountAsync(c => (c.KeeperEmail ?? string.Empty).ToLower() == emailLower
                             || c.Players.Any(p => p.PlayerEmail.ToLower() == emailLower));

        // Kind, а не только владелец: на CampaignPlayerId висят и НПС, заведённые Хранителем,
        // а «сыщик» в кабинете — это лист игрока.
        var characterCount = await db.CharacterStorage
            .CountAsync(c => c.CampaignPlayerId != null
                             && playerIds.Contains(c.CampaignPlayerId.Value)
                             && c.Kind == CharacterKind.PlayerCharacter
                             && c.Status != CharacterStatus.Archived);

        var application = await db.KeeperApplications
            .Where(a => a.UserEmail.ToLower() == emailLower)
            .OrderByDescending(a => a.CreatedAt)
            .FirstOrDefaultAsync();

        return new UserProfile
        {
            Email = email,
            DisplayName = user.UserName ?? email,
            Role = user.Role,
            CampaignCount = campaignCount,
            CharacterCount = characterCount,
            CampaignPlayerCount = playerIds.Count,
            LatestApplication = application
        };
    }

    /// <summary>
    ///     Меняет отображаемое имя. В кампаниях лежит его копия (<c>CampaignPlayer.PlayerName</c>),
    ///     снятая в момент вступления, — <paramref name="syncCampaigns" /> переписывает и её,
    ///     иначе Хранитель за столом продолжит видеть старое имя.
    /// </summary>
    public async Task<Result> UpdateDisplayNameAsync(string displayName, bool syncCampaigns)
    {
        var trimmed = displayName.Trim();
        if (trimmed.Length == 0)
            return Result.Fail("Имя не может быть пустым");
        if (trimmed.Length > MaxDisplayNameLength)
            return Result.Fail($"Имя не длиннее {MaxDisplayNameLength} символов");

        var email = await identityService.GetCurrentUserEmailAsync();
        if (email is null)
            return Result.Fail("Пользователь не авторизован");

        try
        {
            await using var identityDb = await identityDbContextFactory.CreateDbContextAsync();
            var user = await identityDb.Users
                .SingleOrDefaultAsync(u => u.Email != null && u.Email.ToLower() == email.ToLower());
            if (user is null)
                return Result.Fail("Пользователь не найден");

            user.UserName = trimmed;
            await identityDb.SaveChangesAsync();

            if (syncCampaigns)
            {
                var emailLower = email.ToLower();
                await using var db = await dbContextFactory.CreateDbContextAsync();
                await db.CampaignPlayers
                    .Where(p => p.PlayerEmail.ToLower() == emailLower)
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(p => p.PlayerName, trimmed)
                        .SetProperty(p => p.LastUpdated, DateTimeOffset.UtcNow));
            }

            logger.LogInformation("Display name updated for {Email} (syncCampaigns: {Sync})", email, syncCampaigns);
            return Result.Ok();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error updating display name for {Email}", email);
            return Result.Fail("Не удалось сохранить имя");
        }
    }
}
