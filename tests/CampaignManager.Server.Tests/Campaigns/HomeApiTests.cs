using CampaignManager.Core.Campaigns;
using CampaignManager.Core.Characters;
using CampaignManager.Core.Identity;
using Xunit;

namespace CampaignManager.Server.Tests.Campaigns;

/// <summary>Главная одним запросом: мои кампании, доступные, ваншоты, НПС моих кампаний.</summary>
public sealed class HomeApiTests(CampaignsApp app) : IClassFixture<CampaignsApp>
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    // Хранитель видит игроков своей кампании со всеми листами (и того, кто листа не завёл) и НПС кампании;
    // игрок — только свой активный лист и имя Хранителя.
    [Fact]
    public async Task Keeper_sees_players_and_npcs_player_sees_own_sheet()
    {
        TestDatabase.SkipIfMissing();
        var keeper = await app.AddUserAsync(UserRole.Keeper, "Хранитель");
        var player = await app.AddUserAsync(UserRole.Player, "Дмитрий");
        var idle = await app.AddUserAsync(UserRole.Player, "Без листа");
        var campaign = await app.AddCampaignAsync(keeper, player, idle);
        var sheet = await app.AddCharacterAsync(CharacterKind.Player, "Харви Уолтерс", player.Id, campaign.Id);
        await app.AddCharacterAsync(CharacterKind.Player, "Выбывший", player.Id, campaign.Id, status: CharacterStatus.Retired);
        var npc = await app.AddCharacterAsync(CharacterKind.Npc, "Джексон Элиас", campaign: campaign.Id);

        var kept = Assert.Single((await app.Api(keeper).GetHomeAsync(Cancellation)).Mine, c => c.Id == campaign.Id);
        Assert.Equal(CampaignRole.Keeper, kept.MyRole);
        Assert.Equal(2, kept.PlayerCount);
        Assert.Equal(["Дмитрий", "Без листа"], kept.Players.Select(p => p.Name));
        Assert.Equal(["Харви Уолтерс", "Выбывший"], kept.Players[0].Characters.Select(c => c.Name));
        Assert.Empty(kept.Players[1].Characters);
        Assert.Equal(npc.Id, Assert.Single(kept.Npcs).Id);

        var played = Assert.Single((await app.Api(player).GetHomeAsync(Cancellation)).Mine, c => c.Id == campaign.Id);
        Assert.Equal(CampaignRole.Player, played.MyRole);
        Assert.Equal("Хранитель", played.KeeperName);
        Assert.Equal(sheet.Id, played.MyCharacter?.Id);
        Assert.Equal("Антиквар", played.MyCharacter?.Occupation);
        Assert.Empty(played.Players);
        Assert.Empty(played.Npcs);
    }

    // Вступить можно в незавершённую кампанию, где меня нет; ваншоты — бронью прегена, а не здесь.
    [Fact]
    public async Task Available_excludes_mine_completed_and_one_shots()
    {
        TestDatabase.SkipIfMissing();
        var keeper = await app.AddUserAsync(UserRole.Keeper, "Хранитель");
        var player = await app.AddUserAsync();
        var open = await app.AddCampaignAsync(keeper);
        var joined = await app.AddCampaignAsync(keeper, player);
        var completed = await app.AddCampaignAsync(keeper, CampaignStatus.Completed, CampaignKind.Campaign);
        var oneShot = await app.AddCampaignAsync(keeper, CampaignStatus.Planning, CampaignKind.OneShot);

        var available = (await app.Api(player).GetHomeAsync(Cancellation)).Available;

        var row = Assert.Single(available, c => c.Id == open.Id);
        Assert.Equal("Хранитель", row.KeeperName);
        Assert.DoesNotContain(available, c => c.Id == joined.Id || c.Id == completed.Id || c.Id == oneShot.Id);
    }

    // Открытые на запись ваншоты видит любой вошедший: прегены сценария, кто какого занял, мой ли он.
    [Fact]
    public async Task Open_one_shots_list_pregens_and_reservations()
    {
        TestDatabase.SkipIfMissing();
        var keeper = await app.AddUserAsync(UserRole.Keeper, "Хранитель");
        var player = await app.AddUserAsync(UserRole.Player, "Анна");
        var stranger = await app.AddUserAsync();
        var oneShot = await app.AddCampaignAsync(keeper, CampaignStatus.Planning, CampaignKind.OneShot, player);
        var run = await app.AddRunAsync(oneShot.Id, "Эликсир жизни", signupOpen: true);
        var closed = await app.AddRunAsync(oneShot.Id, "Закрытый", signupOpen: false);
        var taken = await app.AddCharacterAsync(CharacterKind.Pregen, "Букинист", scenario: run.ScenarioId);
        var free = await app.AddCharacterAsync(CharacterKind.Pregen, "Доктор", scenario: run.ScenarioId);
        await app.AddCharacterAsync(CharacterKind.Pregen, "Архивный", scenario: run.ScenarioId, status: CharacterStatus.Archived);
        await app.ReserveAsync(run.Id, taken.Id, player.Id);

        var seenByStranger = Assert.Single((await app.Api(stranger).GetHomeAsync(Cancellation)).OneShots, o => o.RunId == run.Id);
        Assert.Equal("Эликсир жизни", seenByStranger.ScenarioName);
        Assert.Equal("Хранитель", seenByStranger.KeeperName);
        Assert.False(seenByStranger.IsMine);
        Assert.Equal(["Букинист", "Доктор"], seenByStranger.Pregens.Select(p => p.Name));
        Assert.Equal("Анна", seenByStranger.Pregens[0].ReservedBy);
        Assert.False(seenByStranger.Pregens[0].IsMine);
        Assert.False(seenByStranger.Pregens[1].IsReserved);
        Assert.DoesNotContain((await app.Api(stranger).GetHomeAsync(Cancellation)).OneShots, o => o.RunId == closed.Id);

        var seenByPlayer = Assert.Single((await app.Api(player).GetHomeAsync(Cancellation)).OneShots, o => o.RunId == run.Id);
        Assert.True(seenByPlayer.Pregens.Single(p => p.Id == taken.Id).IsMine);
        Assert.True(Assert.Single((await app.Api(keeper).GetHomeAsync(Cancellation)).OneShots, o => o.RunId == run.Id).IsMine);
        Assert.Equal(free.Id, seenByPlayer.Pregens[1].Id);
    }
}
