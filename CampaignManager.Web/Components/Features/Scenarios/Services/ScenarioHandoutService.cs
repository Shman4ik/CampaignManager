using CampaignManager.Web.Components.Features.Scenarios.Model;
using CampaignManager.Web.Utilities.DataBase;
using CampaignManager.Web.Utilities.Services;
using Microsoft.EntityFrameworkCore;

namespace CampaignManager.Web.Components.Features.Scenarios.Services;

/// <summary>
///     Раздатка для показа на втором экране и то, кому её показывают: <c>CanManage</c> — смотрит тот,
///     кто ведёт сценарий, и ему можно вернуться к сценарию.
/// </summary>
public sealed record HandoutDisplay(Guid ScenarioId, ScenarioHandout Handout, bool CanManage);

/// <summary>
///     Отдаёт одну раздатку для страницы второго экрана (<c>/scenarios/{id}/handouts/{handoutId}</c>).
///     Отдельный сервис, а не метод <see cref="ScenarioService" />: у того своих проверок прав нет,
///     а эта страница открывается по ссылке и обязана проверить их сама.
///     <list type="bullet">
///         <item><description>ведущий сценария — автор (<c>CreatorEmail</c>), Хранитель его кампании, администратор;</description></item>
///         <item><description>игроки кампании сценария — раздатка предназначена им, это «на руки» на своём устройстве.</description></item>
///     </list>
///     Остальным — <c>null</c>, как и для несуществующей раздатки: перебирать чужие сценарии нельзя.
/// </summary>
public sealed class ScenarioHandoutService(
    IDbContextFactory<AppDbContext> dbContextFactory,
    IdentityService identityService,
    ILogger<ScenarioHandoutService> logger)
{
    public async Task<HandoutDisplay?> GetHandoutForDisplayAsync(Guid scenarioId, Guid handoutId)
    {
        try
        {
            var email = await identityService.GetCurrentUserEmailAsync();
            if (string.IsNullOrEmpty(email))
                return null;

            await using var dbContext = await dbContextFactory.CreateDbContextAsync();
            var scenario = await dbContext.Scenarios
                .AsNoTracking()
                .Include(s => s.Campaign)
                .FirstOrDefaultAsync(s => s.Id == scenarioId);

            if (scenario is null)
                return null;

            var canManage = string.Equals(scenario.CreatorEmail, email, StringComparison.OrdinalIgnoreCase)
                            || string.Equals(scenario.Campaign?.KeeperEmail, email, StringComparison.OrdinalIgnoreCase)
                            || await identityService.IsAdministrator();

            if (!canManage && !await IsCampaignPlayerAsync(dbContext, scenario.CampaignId, email))
            {
                logger.LogWarning("Denied handout {HandoutId} of scenario {ScenarioId} to {UserEmail}",
                    handoutId, scenarioId, email);
                return null;
            }

            var handout = scenario.Handouts.FirstOrDefault(h => h.Id == handoutId);
            return handout is null ? null : new HandoutDisplay(scenario.Id, handout, canManage);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error loading handout {HandoutId} of scenario {ScenarioId}", handoutId, scenarioId);
            return null;
        }
    }

    private static async Task<bool> IsCampaignPlayerAsync(AppDbContext dbContext, Guid? campaignId, string email)
    {
        if (campaignId is null)
            return false;

        var emailLower = email.ToLower();
        return await dbContext.CampaignPlayers
            .AnyAsync(p => p.CampaignId == campaignId.Value && p.PlayerEmail.ToLower() == emailLower);
    }
}
