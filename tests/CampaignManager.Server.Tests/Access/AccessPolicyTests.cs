using CampaignManager.Core.Characters;
using CampaignManager.Server.Access;
using Xunit;

namespace CampaignManager.Server.Tests.Access;

/// <summary>
/// Каждое правило карты прав (AUDIT, «Права»; Server/Access/CLAUDE.md) — на настоящей схеме.
/// Ожидание записано флагами: R — читать, E — править, D — удалять, «-» — ничего (API ответит 404).
/// </summary>
public sealed class AccessPolicyTests(AccessWorld world) : IClassFixture<AccessWorld>
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    // ── Кампания и журнал ──

    [Theory]
    [InlineData("keeper", "RED")]
    [InlineData("player", "R")]
    [InlineData("otherKeeper", "-")]
    [InlineData("outsider", "-")]
    [InlineData("admin", "RED")]
    public async Task Campaign_and_journal_belong_to_members(string user, string expected)
    {
        TestDatabase.SkipIfMissing();
        Assert.Equal(expected, Flags(await world.PolicyFor(user).ForCampaignAsync(world.Campaign, Cancellation)));
    }

    [Theory]
    [InlineData("keeper")]
    [InlineData("admin")]
    public async Task Missing_campaign_is_invisible_even_to_admin(string user)
    {
        TestDatabase.SkipIfMissing();
        Assert.Equal("-", Flags(await world.PolicyFor(user).ForCampaignAsync(Guid.CreateVersion7(), Cancellation)));
    }

    [Theory]
    [InlineData("player", false)]
    [InlineData("keeper", true)]
    [InlineData("admin", true)]
    public async Task Only_keepers_create_campaigns(string user, bool expected)
    {
        TestDatabase.SkipIfMissing();
        Assert.Equal(expected, await world.PolicyFor(user).CanCreateCampaignAsync(Cancellation));
    }

    [Fact]
    public async Task Anyone_joins_open_campaign_once()
    {
        TestDatabase.SkipIfMissing();
        Assert.True(await world.PolicyFor("outsider").CanJoinCampaignAsync(world.Campaign, Cancellation));
        Assert.False(await world.PolicyFor("player").CanJoinCampaignAsync(world.Campaign, Cancellation));
        Assert.False(await world.PolicyFor("outsider").CanJoinCampaignAsync(world.CompletedCampaign, Cancellation));
    }

    // ── Прохождение и бронь ──

    [Theory]
    [InlineData("keeper", "RED")]
    [InlineData("player", "R")]
    [InlineData("otherKeeper", "-")]
    [InlineData("admin", "RED")]
    public async Task Run_follows_its_campaign(string user, string expected)
    {
        TestDatabase.SkipIfMissing();
        Assert.Equal(expected, Flags(await world.PolicyFor(user).ForRunAsync(world.Run, Cancellation)));
    }

    [Fact]
    public async Task Anyone_signed_in_reserves_while_signup_is_open()
    {
        TestDatabase.SkipIfMissing();
        Assert.True(await world.PolicyFor("outsider").CanReserveAsync(world.Run, Cancellation));
        Assert.False(await world.AnonymousPolicy().CanReserveAsync(world.Run, Cancellation));
    }

    // v1: снять бронь мог любой Хранитель — теперь только Хранитель этой кампании.
    [Theory]
    [InlineData("player", "RD")]
    [InlineData("keeper", "RD")]
    [InlineData("otherKeeper", "-")]
    [InlineData("outsider", "-")]
    [InlineData("admin", "RD")]
    public async Task Reservation_is_released_by_its_player_or_campaign_keeper(string user, string expected)
    {
        TestDatabase.SkipIfMissing();
        Assert.Equal(expected, Flags(await world.PolicyFor(user).ForReservationAsync(world.Run, world.Pregen, Cancellation)));
    }

    // ── Сценарий и раздатка ──

    [Theory]
    [InlineData("keeper", "RED")]
    [InlineData("otherKeeper", "RE")]
    [InlineData("player", "-")]
    [InlineData("admin", "RED")]
    public async Task Scenario_is_edited_by_any_keeper_and_deleted_by_author(string user, string expected)
    {
        TestDatabase.SkipIfMissing();
        Assert.Equal(expected, Flags(await world.PolicyFor(user).ForScenarioAsync(world.Scenario, Cancellation)));
    }

    [Theory]
    [InlineData("keeper", "RE")]
    [InlineData("admin", "RED")]
    public async Task Scenario_without_author_is_deleted_only_by_admin(string user, string expected)
    {
        TestDatabase.SkipIfMissing();
        Assert.Equal(expected, Flags(await world.PolicyFor(user).ForScenarioAsync(world.OrphanScenario, Cancellation)));
    }

    [Theory]
    [InlineData("player", false)]
    [InlineData("keeper", true)]
    public async Task Only_keepers_create_scenarios(string user, bool expected)
    {
        TestDatabase.SkipIfMissing();
        Assert.Equal(expected, await world.PolicyFor(user).CanCreateScenarioAsync(Cancellation));
    }

    [Theory]
    [InlineData("keeper", "RED")]
    [InlineData("otherKeeper", "RED")]
    [InlineData("player", "R")]
    [InlineData("outsider", "-")]
    public async Task Handout_is_shown_to_players_of_a_campaign_running_the_scenario(string user, string expected)
    {
        TestDatabase.SkipIfMissing();
        Assert.Equal(expected, Flags(await world.PolicyFor(user).ForHandoutAsync(world.Handout, Cancellation)));
    }

    [Fact]
    public async Task Handout_of_a_scenario_not_played_in_my_campaign_is_invisible()
    {
        TestDatabase.SkipIfMissing();
        Assert.Equal("-", Flags(await world.PolicyFor("player").ForHandoutAsync(world.UnplayedHandout, Cancellation)));
    }

    // ── Лист ──

    [Theory]
    [InlineData("player", "RED")]
    [InlineData("keeper", "RED")]
    [InlineData("otherKeeper", "-")]
    [InlineData("outsider", "-")]
    [InlineData("admin", "RED")]
    public async Task Investigator_belongs_to_owner_and_campaign_keeper(string user, string expected)
    {
        TestDatabase.SkipIfMissing();
        Assert.Equal(expected, Flags(await world.PolicyFor(user).ForCharacterAsync(world.PlayerSheet, Cancellation)));
    }

    [Theory]
    [InlineData("outsider", "RED")]
    [InlineData("keeper", "-")]
    public async Task Investigator_outside_campaign_is_only_owners(string user, string expected)
    {
        TestDatabase.SkipIfMissing();
        Assert.Equal(expected, Flags(await world.PolicyFor(user).ForCharacterAsync(world.OutsiderSheet, Cancellation)));
    }

    [Theory]
    [InlineData("player", "R")]
    [InlineData("outsider", "R")]
    [InlineData("otherKeeper", "RED")]
    public async Task Pregen_is_read_by_everyone_and_edited_by_keepers(string user, string expected)
    {
        TestDatabase.SkipIfMissing();
        Assert.Equal(expected, Flags(await world.PolicyFor(user).ForCharacterAsync(world.Pregen, Cancellation)));
    }

    [Theory]
    [InlineData("keeper", "RED")]
    [InlineData("otherKeeper", "RED")]
    [InlineData("player", "-")]
    public async Task Library_npc_is_keepers_only(string user, string expected)
    {
        TestDatabase.SkipIfMissing();
        Assert.Equal(expected, Flags(await world.PolicyFor(user).ForCharacterAsync(world.LibraryNpc, Cancellation)));
    }

    [Theory]
    [InlineData("keeper", "RED")]
    [InlineData("otherKeeper", "-")]
    [InlineData("player", "-")]
    [InlineData("admin", "RED")]
    public async Task Campaign_npc_is_its_keepers_only(string user, string expected)
    {
        TestDatabase.SkipIfMissing();
        Assert.Equal(expected, Flags(await world.PolicyFor(user).ForCharacterAsync(world.CampaignNpc, Cancellation)));
    }

    // v1: прегена и НПС в чужой сценарий или кампанию клал любой Хранитель.
    [Fact]
    public async Task Creating_characters_respects_campaign_membership()
    {
        TestDatabase.SkipIfMissing();
        Assert.True(await world.PolicyFor("outsider").CanCreateCharacterAsync(CharacterKind.Player, null, Cancellation));
        Assert.True(await world.PolicyFor("player").CanCreateCharacterAsync(CharacterKind.Player, world.Campaign, Cancellation));
        Assert.False(await world.PolicyFor("outsider").CanCreateCharacterAsync(CharacterKind.Player, world.Campaign, Cancellation));
        Assert.False(await world.PolicyFor("player").CanCreateCharacterAsync(CharacterKind.Pregen, null, Cancellation));
        Assert.True(await world.PolicyFor("otherKeeper").CanCreateCharacterAsync(CharacterKind.Pregen, null, Cancellation));
        Assert.True(await world.PolicyFor("otherKeeper").CanCreateCharacterAsync(CharacterKind.Npc, null, Cancellation));
        Assert.False(await world.PolicyFor("otherKeeper").CanCreateCharacterAsync(CharacterKind.Npc, world.Campaign, Cancellation));
        Assert.True(await world.PolicyFor("keeper").CanCreateCharacterAsync(CharacterKind.Npc, world.Campaign, Cancellation));
    }

    // ── Справочники и фонотека ──

    // v1: четыре справочника не проверяли права на сервере вовсе.
    [Theory]
    [InlineData("player", "R")]
    [InlineData("keeper", "RED")]
    [InlineData("admin", "RED")]
    public async Task Catalogs_are_read_by_everyone_and_edited_by_keepers(string user, string expected)
    {
        TestDatabase.SkipIfMissing();
        Assert.Equal(expected, Flags(await world.PolicyFor(user).ForCatalogAsync(Cancellation)));
    }

    [Fact]
    public async Task Player_editing_catalog_is_forbidden()
    {
        TestDatabase.SkipIfMissing();
        var denied = await Assert.ThrowsAsync<AccessDeniedException>(() =>
            world.PolicyFor("player").ForCatalogAsync(Cancellation).Demand(Operation.Edit));
        Assert.Equal(403, denied.StatusCode);
    }

    [Fact]
    public async Task Anonymous_sees_nothing()
    {
        TestDatabase.SkipIfMissing();
        var policy = world.AnonymousPolicy();
        Assert.Equal("-", Flags(await policy.ForCatalogAsync(Cancellation)));
        Assert.Equal("-", Flags(await policy.ForCharacterAsync(world.Pregen, Cancellation)));
        Assert.Equal("-", Flags(await policy.ForCampaignAsync(world.Campaign, Cancellation)));
    }

    // ── Сцена и админка ──

    [Theory]
    [InlineData("keeper", "RED")]
    [InlineData("otherKeeper", "-")]
    [InlineData("player", "-")]
    [InlineData("admin", "RED")]
    public async Task Encounter_is_its_keepers(string user, string expected)
    {
        TestDatabase.SkipIfMissing();
        Assert.Equal(expected, Flags(await world.PolicyFor(user).ForEncounterAsync(world.Encounter, Cancellation)));
    }

    [Fact]
    public async Task Encounter_in_campaign_is_started_by_its_keeper()
    {
        TestDatabase.SkipIfMissing();
        Assert.True(await world.PolicyFor("keeper").CanStartEncounterAsync(world.Campaign, Cancellation));
        Assert.True(await world.PolicyFor("otherKeeper").CanStartEncounterAsync(null, Cancellation));
        Assert.False(await world.PolicyFor("otherKeeper").CanStartEncounterAsync(world.Campaign, Cancellation));
        Assert.False(await world.PolicyFor("player").CanStartEncounterAsync(null, Cancellation));
    }

    [Theory]
    [InlineData("admin", true)]
    [InlineData("keeper", false)]
    [InlineData("player", false)]
    public async Task Only_admin_administers(string user, bool expected)
    {
        TestDatabase.SkipIfMissing();
        Assert.Equal(expected, await world.PolicyFor(user).CanAdministerAsync(Cancellation));
    }

    // ── Demand ──

    // «Нет объекта» и «нет доступа» неразличимы: чужой лист и журнал — 404, а не 403.
    [Fact]
    public async Task Foreign_sheet_is_not_found_rather_than_forbidden()
    {
        TestDatabase.SkipIfMissing();
        var denied = await Assert.ThrowsAsync<AccessDeniedException>(() =>
            world.PolicyFor("otherKeeper").ForCharacterAsync(world.PlayerSheet, Cancellation).Demand(Operation.Read));
        Assert.Equal(404, denied.StatusCode);

        var journal = await Assert.ThrowsAsync<AccessDeniedException>(() =>
            world.PolicyFor("outsider").ForCampaignAsync(world.Campaign, Cancellation).Demand(Operation.Edit));
        Assert.Equal(404, journal.StatusCode);
    }

    [Fact]
    public async Task Player_writing_journal_is_forbidden()
    {
        TestDatabase.SkipIfMissing();
        var denied = await Assert.ThrowsAsync<AccessDeniedException>(() =>
            world.PolicyFor("player").ForCampaignAsync(world.Campaign, Cancellation).Demand(Operation.Edit));
        Assert.Equal(403, denied.StatusCode);
    }

    private static string Flags(Server.Access.Access access)
    {
        var flags = $"{(access.CanRead ? "R" : "")}{(access.CanEdit ? "E" : "")}{(access.CanDelete ? "D" : "")}";
        return flags.Length == 0 ? "-" : flags;
    }
}
