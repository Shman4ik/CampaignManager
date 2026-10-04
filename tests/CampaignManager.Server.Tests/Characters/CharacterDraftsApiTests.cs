using System.Net;
using CampaignManager.ApiClient.Characters;
using CampaignManager.ApiClient.Scenarios;
using CampaignManager.Contracts.Characters;
using CampaignManager.Contracts.Platform;
using CampaignManager.Core.Characters;
using CampaignManager.Core.Identity;
using CampaignManager.Data.Identity;
using CampaignManager.Server.Tests.Campaigns;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CampaignManager.Server.Tests.Characters;

/// <summary>
/// Черновик помощника на сервере: игрок продолжает с другого устройства (запись с версией, как у листа), Хранитель кампании
/// видит шаг на главной и странице кампании и читает черновик, посторонний — нет; черновик уходит с созданием листа, по
/// «Начать заново», с выходом из кампании и с бронью прегена.
/// </summary>
public sealed class CharacterDraftsApiTests(CampaignsApp app) : IClassFixture<CampaignsApp>
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private CharacterDraftsApiClient Drafts(User user) => new(app.Http(user));

    private static async Task<ApiException> Fails(HttpStatusCode status, Func<Task> call)
    {
        var error = await Assert.ThrowsAsync<ApiException>(call);
        Assert.Equal(status, error.StatusCode);
        return error;
    }

    private static InvestigatorDraft Draft(string name, CreationStep step) => new()
    {
        StepIndex = (int)step,
        Personal = new PersonalInfo { Name = name },
    };

    [Fact]
    public async Task Player_continues_the_draft_from_another_device_and_a_stale_write_is_refused()
    {
        TestDatabase.SkipIfMissing();
        var keeper = await app.AddUserAsync(UserRole.Keeper);
        var player = await app.AddUserAsync(UserRole.Player, "Игрок");
        var campaign = await app.AddCampaignAsync(keeper, player);
        var phone = Drafts(player);
        var tablet = Drafts(player);

        Assert.Null(await phone.GetMineAsync(campaign.Id, Cancellation));

        var first = await phone.SaveAsync(campaign.Id, Draft("Харви", CreationStep.Characteristics), null, Cancellation);
        var onTablet = await tablet.GetMineAsync(campaign.Id, Cancellation);
        Assert.NotNull(onTablet);
        Assert.Equal("Харви", onTablet.Draft.Personal.Name);
        Assert.Equal(first.Version, onTablet.Version);
        Assert.Equal(player.Id, onTablet.OwnerId);

        var second = await tablet.SaveAsync(campaign.Id, Draft("Харви Уолтерс", CreationStep.Skills), onTablet.Version, Cancellation);
        Assert.NotEqual(first.Version, second.Version);

        // Телефон правит на старой версии — 409, а не тихая перезапись того, что сделано на планшете.
        var stale = await Fails(HttpStatusCode.Conflict,
            () => phone.SaveAsync(campaign.Id, Draft("Харви", CreationStep.Occupation), first.Version, Cancellation));
        Assert.Equal(ApiProblemCodes.Stale, stale.Code);
        // Черновик уже есть — без версии нельзя.
        await Fails(HttpStatusCode.PreconditionRequired,
            () => phone.SaveAsync(campaign.Id, Draft("Харви", CreationStep.Occupation), null, Cancellation));

        var read = await phone.GetMineAsync(campaign.Id, Cancellation);
        Assert.Equal((int)CreationStep.Skills, read!.Draft.StepIndex);
        Assert.Equal("Харви Уолтерс", read.Draft.Personal.Name);
    }

    [Fact]
    public async Task Keeper_and_admin_read_the_draft_and_a_stranger_or_another_player_does_not()
    {
        TestDatabase.SkipIfMissing();
        var keeper = await app.AddUserAsync(UserRole.Keeper);
        var player = await app.AddUserAsync(UserRole.Player);
        var neighbour = await app.AddUserAsync(UserRole.Player);
        var otherKeeper = await app.AddUserAsync(UserRole.Keeper);
        var admin = await app.AddUserAsync(UserRole.Admin);
        var campaign = await app.AddCampaignAsync(keeper, player, neighbour);
        await Drafts(player).SaveAsync(campaign.Id, Draft("Ада", CreationStep.Biography), null, Cancellation);

        Assert.Equal("Ада", (await Drafts(keeper).GetAsync(campaign.Id, player.Id, Cancellation)).Draft.Personal.Name);
        Assert.Equal("Ада", (await Drafts(admin).GetAsync(campaign.Id, player.Id, Cancellation)).Draft.Personal.Name);
        Assert.Equal("Ада", (await Drafts(player).GetAsync(campaign.Id, player.Id, Cancellation)).Draft.Personal.Name);
        await Fails(HttpStatusCode.NotFound, () => Drafts(neighbour).GetAsync(campaign.Id, player.Id, Cancellation));
        await Fails(HttpStatusCode.NotFound, () => Drafts(otherKeeper).GetAsync(campaign.Id, player.Id, Cancellation));
        // Нет черновика — тоже 404, неотличимо от «чужой».
        await Fails(HttpStatusCode.NotFound, () => Drafts(keeper).GetAsync(campaign.Id, neighbour.Id, Cancellation));

        // Пишет только участник — и только свой; посторонний не заводит черновик в чужой кампании.
        await Fails(HttpStatusCode.Forbidden,
            () => Drafts(otherKeeper).SaveAsync(campaign.Id, Draft("Чужой", CreationStep.Method), null, Cancellation));
        Assert.Null(await Drafts(neighbour).GetMineAsync(campaign.Id, Cancellation));
    }

    [Fact]
    public async Task Keeper_sees_the_step_on_home_and_campaign_page_and_the_player_sees_only_their_own()
    {
        TestDatabase.SkipIfMissing();
        var keeper = await app.AddUserAsync(UserRole.Keeper);
        var player = await app.AddUserAsync(UserRole.Player);
        var neighbour = await app.AddUserAsync(UserRole.Player);
        var campaign = await app.AddCampaignAsync(keeper, player, neighbour);
        await Drafts(player).SaveAsync(campaign.Id, Draft("Ада", CreationStep.Skills), null, Cancellation);
        await Drafts(neighbour).SaveAsync(campaign.Id, Draft("Бо", CreationStep.Method), null, Cancellation);

        var keeperHome = (await app.Api(keeper).GetHomeAsync(Cancellation)).Mine.Single(c => c.Id == campaign.Id);
        var playerRow = keeperHome.Players.Single(p => p.UserId == player.Id);
        Assert.Equal((int)CreationStep.Skills, playerRow.Draft?.Step);
        Assert.Equal((int)CreationStep.Method, keeperHome.Players.Single(p => p.UserId == neighbour.Id).Draft?.Step);
        Assert.Null(keeperHome.MyDraft);

        var playerHome = (await app.Api(player).GetHomeAsync(Cancellation)).Mine.Single(c => c.Id == campaign.Id);
        Assert.Equal((int)CreationStep.Skills, playerHome.MyDraft?.Step);
        Assert.Empty(playerHome.Players);

        var keeperPage = await app.Api(keeper).GetCampaignAsync(campaign.Id, Cancellation);
        Assert.Equal((int)CreationStep.Skills, keeperPage.Members.Single(m => m.UserId == player.Id).Draft?.Step);
        var playerPage = await app.Api(player).GetCampaignAsync(campaign.Id, Cancellation);
        Assert.Equal((int)CreationStep.Skills, playerPage.Members.Single(m => m.UserId == player.Id).Draft?.Step);
        Assert.Null(playerPage.Members.Single(m => m.UserId == neighbour.Id).Draft);
    }

    [Fact]
    public async Task Creating_the_sheet_and_restart_remove_the_draft()
    {
        TestDatabase.SkipIfMissing();
        var keeper = await app.AddUserAsync(UserRole.Keeper);
        var player = await app.AddUserAsync(UserRole.Player);
        var campaign = await app.AddCampaignAsync(keeper, player);
        var drafts = Drafts(player);

        await drafts.SaveAsync(campaign.Id, Draft("Ада", CreationStep.Gear), null, Cancellation);
        await drafts.DeleteAsync(campaign.Id, Cancellation);
        Assert.Null(await drafts.GetMineAsync(campaign.Id, Cancellation));
        await drafts.DeleteAsync(campaign.Id, Cancellation); // стирать нечего — не ошибка

        var saved = await drafts.SaveAsync(campaign.Id, Draft("Ада", CreationStep.Summary), null, Cancellation);
        await new CharactersApiClient(app.Http(player)).CreateAsync(new CreateCharacterRequest
        {
            Kind = CharacterKind.Player,
            CampaignId = campaign.Id,
            Sheet = new CharacterSheet { Personal = new PersonalInfo { Name = "Ада" } },
        }, Cancellation);

        Assert.Null(await drafts.GetMineAsync(campaign.Id, Cancellation));
        // Другое устройство, правившее старый черновик, получает 409: лист уже создан, молча заводить черновик заново нельзя.
        await Fails(HttpStatusCode.Conflict, () => drafts.SaveAsync(campaign.Id, Draft("Ада", CreationStep.Summary), saved.Version, Cancellation));
    }

    [Fact]
    public async Task Leaving_the_campaign_removes_the_draft()
    {
        TestDatabase.SkipIfMissing();
        var keeper = await app.AddUserAsync(UserRole.Keeper);
        var player = await app.AddUserAsync(UserRole.Player);
        var campaign = await app.AddCampaignAsync(keeper, player);
        await Drafts(player).SaveAsync(campaign.Id, Draft("Ада", CreationStep.Occupation), null, Cancellation);

        await app.Api(player).RemoveMemberAsync(campaign.Id, player.Id, Cancellation);

        await using var db = app.Database.CreateContext();
        Assert.False(await db.CharacterDrafts.AnyAsync(d => d.CampaignId == campaign.Id && d.OwnerId == player.Id, Cancellation));
    }

    [Fact]
    public async Task Taking_a_pregen_place_removes_the_draft_in_that_game()
    {
        TestDatabase.SkipIfMissing();
        var keeper = await app.AddUserAsync(UserRole.Keeper);
        var player = await app.AddUserAsync(UserRole.Player);
        var campaign = await app.AddCampaignAsync(keeper, player);
        var run = await app.AddRunAsync(campaign.Id, signupOpen: true);
        var pregen = await app.AddCharacterAsync(CharacterKind.Pregen, "Готовый", scenario: run.ScenarioId);
        await Drafts(player).SaveAsync(campaign.Id, Draft("Ада", CreationStep.Characteristics), null, Cancellation);

        await new RunsApiClient(app.Http(player)).ReserveAsync(run.Id, pregen.Id, Cancellation);

        Assert.Null(await Drafts(player).GetMineAsync(campaign.Id, Cancellation));
    }

    [Fact]
    public async Task Draft_with_an_unknown_step_is_400()
    {
        TestDatabase.SkipIfMissing();
        var keeper = await app.AddUserAsync(UserRole.Keeper);
        var player = await app.AddUserAsync(UserRole.Player);
        var campaign = await app.AddCampaignAsync(keeper, player);

        await Fails(HttpStatusCode.BadRequest,
            () => Drafts(player).SaveAsync(campaign.Id, new InvestigatorDraft { StepIndex = 7 }, null, Cancellation));
        await Fails(HttpStatusCode.BadRequest,
            () => Drafts(player).SaveAsync(campaign.Id, Draft(new string('я', CharacterLimits.NameLength + 1), CreationStep.Method), null, Cancellation));
    }
}
