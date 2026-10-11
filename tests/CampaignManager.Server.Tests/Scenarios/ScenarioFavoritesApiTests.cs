using System.Net;
using CampaignManager.ApiClient.Scenarios;
using CampaignManager.Contracts.Platform;
using CampaignManager.Contracts.Scenarios;
using CampaignManager.Core.Identity;
using CampaignManager.Data.Identity;
using CampaignManager.Server.Tests.Campaigns;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CampaignManager.Server.Tests.Scenarios;

/// <summary>
/// Избранные сценарии: личная звёздочка Хранителя. Своя у каждого пользователя, игроку сценарий не виден (404),
/// отметка идемпотентна и не пишет сам сценарий, а удаление сценария и пользователя уносит строку каскадом.
/// </summary>
public sealed class ScenarioFavoritesApiTests(CampaignsApp app) : IClassFixture<CampaignsApp>
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private ScenariosApiClient Api(User user) => new(app.Http(user));

    private static async Task Fails(HttpStatusCode status, Func<Task> call)
    {
        var error = await Assert.ThrowsAsync<ApiException>(call);
        Assert.Equal(status, error.StatusCode);
    }

    private async Task<(User Keeper, ScenarioDto Scenario)> NewScenarioAsync(string name)
    {
        var keeper = await app.AddUserAsync(UserRole.Keeper, "Автор");
        return (keeper, await Api(keeper).CreateAsync(new ScenarioInput(name), Cancellation));
    }

    [Fact]
    public async Task Star_is_set_and_removed_and_list_puts_favorites_first()
    {
        TestDatabase.SkipIfMissing();
        var (keeper, first) = await NewScenarioAsync("Яма");
        var api = Api(keeper);
        var second = await api.CreateAsync(new ScenarioInput("Башня"), Cancellation);
        Assert.False(first.IsFavorite);

        await api.SetFavoriteAsync(first.Id, true, Cancellation);

        Assert.True((await api.GetAsync(first.Id, Cancellation)).IsFavorite);
        Assert.False((await api.GetAsync(second.Id, Cancellation)).IsFavorite);
        var mine = (await api.ListAsync(Cancellation)).Items.Where(s => s.Id == first.Id || s.Id == second.Id).ToList();
        // «Башня» по алфавиту раньше «Ямы», но звёздочка ставит «Яму» выше.
        Assert.Equal([first.Id, second.Id], mine.Select(s => s.Id));
        Assert.True(mine[0].IsFavorite);
        Assert.False(mine[1].IsFavorite);

        await api.SetFavoriteAsync(first.Id, false, Cancellation);

        Assert.False((await api.GetAsync(first.Id, Cancellation)).IsFavorite);
        var after = (await api.ListAsync(Cancellation)).Items.Where(s => s.Id == first.Id || s.Id == second.Id).ToList();
        Assert.Equal([second.Id, first.Id], after.Select(s => s.Id));
        Assert.All(after, s => Assert.False(s.IsFavorite));
    }

    [Fact]
    public async Task Star_is_personal_each_keeper_has_their_own()
    {
        TestDatabase.SkipIfMissing();
        var (author, scenario) = await NewScenarioAsync("Общий");
        var other = await app.AddUserAsync(UserRole.Keeper);
        var admin = await app.AddUserAsync(UserRole.Admin);

        await Api(author).SetFavoriteAsync(scenario.Id, true, Cancellation);

        Assert.True((await Api(author).GetAsync(scenario.Id, Cancellation)).IsFavorite);
        Assert.False((await Api(other).GetAsync(scenario.Id, Cancellation)).IsFavorite);
        Assert.False((await Api(other).ListAsync(Cancellation)).Items.Single(s => s.Id == scenario.Id).IsFavorite);
        Assert.False((await Api(admin).ListAsync(Cancellation)).Items.Single(s => s.Id == scenario.Id).IsFavorite);

        // Чужой Хранитель ставит и снимает свою, авторская от этого не меняется.
        await Api(other).SetFavoriteAsync(scenario.Id, true, Cancellation);
        await Api(other).SetFavoriteAsync(scenario.Id, false, Cancellation);

        Assert.True((await Api(author).ListAsync(Cancellation)).Items.Single(s => s.Id == scenario.Id).IsFavorite);
        Assert.False((await Api(other).ListAsync(Cancellation)).Items.Single(s => s.Id == scenario.Id).IsFavorite);
    }

    [Fact]
    public async Task Setting_and_removing_are_idempotent_and_do_not_touch_the_scenario()
    {
        TestDatabase.SkipIfMissing();
        var (keeper, scenario) = await NewScenarioAsync("Тихий");
        var api = Api(keeper);
        var before = await api.GetAsync(scenario.Id, Cancellation);

        await api.SetFavoriteAsync(scenario.Id, false, Cancellation); // снять то, чего нет
        await api.SetFavoriteAsync(scenario.Id, true, Cancellation);
        await api.SetFavoriteAsync(scenario.Id, true, Cancellation); // повтор

        var after = await api.GetAsync(scenario.Id, Cancellation);
        Assert.True(after.IsFavorite);
        // Звёздочка — не правка сценария: версия корня и «Изменён» прежние, прежний If-Match ещё годится.
        Assert.Equal(before.Version, after.Version);
        Assert.Equal(before.UpdatedAt, after.UpdatedAt);
        await api.UpdateAsync(scenario.Id, new ScenarioInput("Тихий, правка"), before.Version, Cancellation);

        await using var db = app.Database.CreateContext();
        Assert.Equal(1, await db.ScenarioFavorites.CountAsync(f => f.ScenarioId == scenario.Id && f.UserId == keeper.Id, Cancellation));
    }

    [Fact]
    public async Task Player_and_missing_scenario_get_404()
    {
        TestDatabase.SkipIfMissing();
        var (_, scenario) = await NewScenarioAsync("Закрытый");
        var player = Api(await app.AddUserAsync());
        var keeper = Api(await app.AddUserAsync(UserRole.Keeper));

        await Fails(HttpStatusCode.NotFound, () => player.SetFavoriteAsync(scenario.Id, true, Cancellation));
        await Fails(HttpStatusCode.NotFound, () => player.SetFavoriteAsync(scenario.Id, false, Cancellation));
        await Fails(HttpStatusCode.NotFound, () => keeper.SetFavoriteAsync(Guid.NewGuid(), true, Cancellation));
        await Fails(HttpStatusCode.NotFound, () => keeper.SetFavoriteAsync(Guid.NewGuid(), false, Cancellation));

        await using var db = app.Database.CreateContext();
        Assert.False(await db.ScenarioFavorites.AnyAsync(f => f.ScenarioId == scenario.Id, Cancellation));
    }

    [Fact]
    public async Task Deleting_the_scenario_or_the_user_removes_their_stars()
    {
        TestDatabase.SkipIfMissing();
        var (author, scenario) = await NewScenarioAsync("Исчезнет");
        var other = await app.AddUserAsync(UserRole.Keeper);
        var survivor = await Api(author).CreateAsync(new ScenarioInput("Останется"), Cancellation);
        await Api(author).SetFavoriteAsync(scenario.Id, true, Cancellation);
        await Api(other).SetFavoriteAsync(scenario.Id, true, Cancellation);
        await Api(other).SetFavoriteAsync(survivor.Id, true, Cancellation);

        await Api(author).DeleteAsync(scenario.Id, Cancellation);

        await using var db = app.Database.CreateContext();
        Assert.False(await db.ScenarioFavorites.AnyAsync(f => f.ScenarioId == scenario.Id, Cancellation));
        Assert.True(await db.ScenarioFavorites.AnyAsync(f => f.ScenarioId == survivor.Id && f.UserId == other.Id, Cancellation));
        await db.Users.Where(u => u.Id == other.Id).ExecuteDeleteAsync(Cancellation);
        Assert.False(await db.ScenarioFavorites.AnyAsync(f => f.UserId == other.Id, Cancellation));
    }
}
