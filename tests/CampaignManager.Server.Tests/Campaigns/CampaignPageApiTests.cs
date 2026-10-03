using System.Net;
using CampaignManager.Contracts.Campaigns;
using CampaignManager.Core.Campaigns;
using CampaignManager.Core.Characters;
using CampaignManager.Core.Identity;
using Xunit;

namespace CampaignManager.Server.Tests.Campaigns;

/// <summary>
/// Страница кампании (UX-2 X1): сыщики участников, НПС, прохождения, последняя встреча и ссылка-приглашение. Права те же,
/// что у самой кампании: правящий видит всё, игрок — только свой активный лист, ни НПС, ни прохождений.
/// </summary>
public sealed class CampaignPageApiTests(CampaignsApp app) : IClassFixture<CampaignsApp>
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Keeper_sees_every_sheet_npcs_and_runs()
    {
        TestDatabase.SkipIfMissing();
        var keeper = await app.AddUserAsync(UserRole.Keeper, "Хранитель");
        var alice = await app.AddUserAsync(UserRole.Player, "Алиса");
        var bob = await app.AddUserAsync(UserRole.Player, "Боб");
        var campaign = await app.AddCampaignAsync(keeper, alice, bob);
        var harvey = await app.AddCharacterAsync(CharacterKind.Player, "Харви", alice.Id, campaign.Id);
        var retired = await app.AddCharacterAsync(CharacterKind.Player, "Прежний", alice.Id, campaign.Id, status: CharacterStatus.Retired);
        await app.AddCharacterAsync(CharacterKind.Player, "Чужой лист без кампании", alice.Id);
        var npc = await app.AddCharacterAsync(CharacterKind.Npc, "Лавкрафт", campaign: campaign.Id);
        var run = await app.AddRunAsync(campaign.Id, "Дом с привидениями");

        var details = await app.Api(keeper).GetCampaignAsync(campaign.Id, Cancellation);

        var aliceRow = Assert.Single(details.Members, m => m.UserId == alice.Id);
        Assert.Equal(2, aliceRow.Characters.Count);
        Assert.Contains(aliceRow.Characters, c => c.Id == harvey.Id && c.Name == "Харви");
        Assert.Contains(aliceRow.Characters, c => c.Id == retired.Id && c.Status == CharacterStatus.Retired);
        Assert.Empty(Assert.Single(details.Members, m => m.UserId == bob.Id).Characters);
        Assert.Empty(Assert.Single(details.Members, m => m.UserId == keeper.Id).Characters);
        Assert.Equal(npc.Id, Assert.Single(details.Npcs).Id);
        var runRow = Assert.Single(details.Runs);
        Assert.Equal(run.Id, runRow.RunId);
        Assert.Equal("Дом с привидениями", runRow.ScenarioName);
        Assert.Null(details.LastSession);
    }

    // Лист другого игрока игроку не открывается (ForCharacter), значит и называть его на странице незачем; НПС и название
    // сценария — тайна Хранителя.
    [Fact]
    public async Task Player_sees_only_own_active_sheet_and_no_npcs_or_runs()
    {
        TestDatabase.SkipIfMissing();
        var keeper = await app.AddUserAsync(UserRole.Keeper);
        var alice = await app.AddUserAsync(UserRole.Player, "Алиса");
        var bob = await app.AddUserAsync(UserRole.Player, "Боб");
        var campaign = await app.AddCampaignAsync(keeper, alice, bob);
        var mine = await app.AddCharacterAsync(CharacterKind.Player, "Харви", alice.Id, campaign.Id);
        await app.AddCharacterAsync(CharacterKind.Player, "Выбывший", alice.Id, campaign.Id, status: CharacterStatus.Retired);
        await app.AddCharacterAsync(CharacterKind.Player, "Чужой", bob.Id, campaign.Id);
        await app.AddCharacterAsync(CharacterKind.Npc, "Тайный НПС", campaign: campaign.Id);
        await app.AddRunAsync(campaign.Id, "Секретный сценарий");

        var raw = await app.Http(alice).GetStringAsync(CampaignsRoutes.Campaign(campaign.Id), Cancellation);
        var details = await app.Api(alice).GetCampaignAsync(campaign.Id, Cancellation);

        Assert.Equal(mine.Id, Assert.Single(Assert.Single(details.Members, m => m.UserId == alice.Id).Characters).Id);
        Assert.Empty(Assert.Single(details.Members, m => m.UserId == bob.Id).Characters);
        Assert.Empty(details.Npcs);
        Assert.Empty(details.Runs);
        Assert.DoesNotContain("Чужой", raw);
        Assert.DoesNotContain("Тайный НПС", raw);
        Assert.DoesNotContain("Секретный сценарий", raw);
        Assert.DoesNotContain("@", raw);
    }

    [Fact]
    public async Task Admin_who_is_not_a_member_sees_everything_and_outsider_gets_404()
    {
        TestDatabase.SkipIfMissing();
        var keeper = await app.AddUserAsync(UserRole.Keeper);
        var player = await app.AddUserAsync();
        var admin = await app.AddUserAsync(UserRole.Admin);
        var outsider = await app.AddUserAsync();
        var campaign = await app.AddCampaignAsync(keeper, player);
        await app.AddCharacterAsync(CharacterKind.Npc, "НПС", campaign: campaign.Id);
        await app.AddRunAsync(campaign.Id);

        var details = await app.Api(admin).GetCampaignAsync(campaign.Id, Cancellation);

        Assert.Single(details.Npcs);
        Assert.Single(details.Runs);
        await ApiAssert.FailsWith(HttpStatusCode.NotFound, () => app.Api(outsider).GetCampaignAsync(campaign.Id, Cancellation));
    }

    [Fact]
    public async Task Last_session_is_the_latest_by_date_then_number()
    {
        TestDatabase.SkipIfMissing();
        var keeper = await app.AddUserAsync(UserRole.Keeper);
        var player = await app.AddUserAsync();
        var campaign = await app.AddCampaignAsync(keeper, player);
        var journal = app.Api(keeper);
        await journal.AddSessionAsync(campaign.Id, new(new DateOnly(2026, 9, 1), 1, "Первая", null, null, null, false), Cancellation);
        var later = await journal.AddSessionAsync(campaign.Id, new(new DateOnly(2026, 9, 20), 2, "Вторая", null, null, null, false), Cancellation);
        await journal.AddSessionAsync(campaign.Id, new(new DateOnly(2026, 9, 10), 3, "Между", null, null, null, false), Cancellation);

        var asPlayer = await app.Api(player).GetCampaignAsync(campaign.Id, Cancellation);

        var last = Assert.IsType<CampaignLastSessionDto>(asPlayer.LastSession);
        Assert.Equal(later.Id, last.Id);
        Assert.Equal(2, last.Number);
        Assert.Equal("Вторая", last.Title);
    }

    // Ссылка-приглашение: страница /join/{id} показывает, куда зовут. Видна любому вошедшему, пока кампания не завершена —
    // как и в «Можно вступить» на главной; участнику — всегда; завершённая постороннему — 404.
    [Fact]
    public async Task Invite_shows_campaign_to_any_signed_in_user_until_it_is_completed()
    {
        TestDatabase.SkipIfMissing();
        var keeper = await app.AddUserAsync(UserRole.Keeper, "Хранитель Иван");
        var player = await app.AddUserAsync();
        var newcomer = await app.AddUserAsync();
        var campaign = await app.AddCampaignAsync(keeper, player);

        var invite = await app.Api(newcomer).GetInviteAsync(campaign.Id, Cancellation);

        Assert.Equal(campaign.Name, invite.Name);
        Assert.Equal("Хранитель Иван", invite.KeeperName);
        Assert.Equal(1, invite.PlayerCount);
        Assert.False(invite.IsMember);
        Assert.True(invite.CanJoin);

        var member = await app.Api(player).GetInviteAsync(campaign.Id, Cancellation);
        Assert.True(member.IsMember);
        Assert.False(member.CanJoin);

        var joined = await app.Api(newcomer).JoinAsync(campaign.Id, new JoinCampaignRequest(null), Cancellation);
        Assert.Equal(2, joined.Campaign.PlayerCount);
    }

    [Fact]
    public async Task Invite_of_completed_or_missing_campaign_is_404_for_outsiders_but_open_to_members()
    {
        TestDatabase.SkipIfMissing();
        var keeper = await app.AddUserAsync(UserRole.Keeper);
        var player = await app.AddUserAsync();
        var outsider = await app.AddUserAsync();
        var done = await app.AddCampaignAsync(keeper, CampaignStatus.Completed, CampaignKind.Campaign, player);

        await ApiAssert.FailsWith(HttpStatusCode.NotFound, () => app.Api(outsider).GetInviteAsync(done.Id, Cancellation));
        await ApiAssert.FailsWith(HttpStatusCode.NotFound, () => app.Api(outsider).GetInviteAsync(Guid.NewGuid(), Cancellation));
        await ApiAssert.FailsWith(HttpStatusCode.Unauthorized, () => app.Api(null).GetInviteAsync(done.Id, Cancellation));
        Assert.True((await app.Api(player).GetInviteAsync(done.Id, Cancellation)).IsMember);
    }

    [Fact]
    public async Task Invite_does_not_leak_emails()
    {
        TestDatabase.SkipIfMissing();
        var keeper = await app.AddUserAsync(UserRole.Keeper);   // имя профиля = почта, как при входе без name
        var newcomer = await app.AddUserAsync();
        var campaign = await app.AddCampaignAsync(keeper);

        var raw = await app.Http(newcomer).GetStringAsync(CampaignsRoutes.Invite(campaign.Id), Cancellation);

        Assert.DoesNotContain("@", raw);
    }
}
