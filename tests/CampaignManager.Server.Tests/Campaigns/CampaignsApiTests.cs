using System.Net;
using CampaignManager.Contracts.Campaigns;
using CampaignManager.Core;
using CampaignManager.Core.Campaigns;
using CampaignManager.Core.Characters;
using CampaignManager.Core.Identity;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CampaignManager.Server.Tests.Campaigns;

/// <summary>Кампании и участники через API: права по таблице Access, псевдонимы, выход, удаление.</summary>
public sealed class CampaignsApiTests(CampaignsApp app) : IClassFixture<CampaignsApp>
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private static CampaignInput Input(string name = "Маски Ньярлатхотепа", CampaignStatus status = CampaignStatus.Planning) =>
        new(name, CampaignKind.Campaign, status, Era.Classic);

    // Хранитель — участник с ролью: свою кампанию он видит сразу, без «вступления» (в v1 — только вступив сам).
    [Fact]
    public async Task Keeper_creates_campaign_and_sees_it_at_once()
    {
        TestDatabase.SkipIfMissing();
        var keeper = await app.AddUserAsync(UserRole.Keeper, "Хранитель");

        var created = await app.Api(keeper).CreateCampaignAsync(Input("  Маски  "), Cancellation);

        Assert.Equal("Маски", created.Name);
        Assert.Equal(CampaignRole.Keeper, created.MyRole);
        Assert.True(created.CanEdit);
        Assert.True(created.CanDelete);
        Assert.Contains(await app.Api(keeper).GetCampaignsAsync(Cancellation), c => c.Id == created.Id);

        var home = await app.Api(keeper).GetHomeAsync(Cancellation);
        var mine = Assert.Single(home.Mine, c => c.Id == created.Id);
        Assert.Equal(CampaignRole.Keeper, mine.MyRole);
        Assert.Null(mine.KeeperName);
    }

    // v1 создавал кампанию кому угодно.
    [Fact]
    public async Task Player_cannot_create_campaign()
    {
        TestDatabase.SkipIfMissing();
        var player = await app.AddUserAsync(UserRole.Player);

        await ApiAssert.FailsWith(HttpStatusCode.Forbidden, () => app.Api(player).CreateCampaignAsync(Input(), Cancellation));
    }

    [Fact]
    public async Task Empty_name_is_rejected_with_text()
    {
        TestDatabase.SkipIfMissing();
        var keeper = await app.AddUserAsync(UserRole.Keeper);

        var message = await ApiAssert.FailsWith(HttpStatusCode.BadRequest, () => app.Api(keeper).CreateCampaignAsync(Input("   "), Cancellation));
        Assert.Contains("Название", message);
    }

    // Править и удалять — Хранитель кампании и администратор (в v1 администратор чужие кампании не правил).
    // Чужой Хранитель кампании не видит вовсе — 404, игрок видит, но править не может — 403.
    [Fact]
    public async Task Only_campaign_keeper_or_admin_edits_and_deletes()
    {
        TestDatabase.SkipIfMissing();
        var keeper = await app.AddUserAsync(UserRole.Keeper);
        var otherKeeper = await app.AddUserAsync(UserRole.Keeper);
        var player = await app.AddUserAsync();
        var admin = await app.AddUserAsync(UserRole.Admin);
        var campaign = await app.AddCampaignAsync(keeper, player);

        await ApiAssert.FailsWith(HttpStatusCode.NotFound,
            () => app.Api(otherKeeper).UpdateCampaignAsync(campaign.Id, Input("Чужая"), Cancellation));
        await ApiAssert.FailsWith(HttpStatusCode.Forbidden,
            () => app.Api(player).UpdateCampaignAsync(campaign.Id, Input("Моя"), Cancellation));
        await ApiAssert.FailsWith(HttpStatusCode.Forbidden, () => app.Api(player).DeleteCampaignAsync(campaign.Id, Cancellation));

        var renamed = await app.Api(admin).UpdateCampaignAsync(campaign.Id, Input("Правка админа", CampaignStatus.OnHold), Cancellation);
        Assert.Equal("Правка админа", renamed.Name);
        Assert.Equal(CampaignStatus.OnHold, renamed.Status);
        Assert.Null(renamed.MyRole);
        Assert.Contains(await app.Api(admin).GetCampaignsAsync(Cancellation), c => c.Id == campaign.Id);

        await app.Api(keeper).UpdateCampaignAsync(campaign.Id, Input("Правка Хранителя"), Cancellation);
        await app.Api(admin).DeleteCampaignAsync(campaign.Id, Cancellation);
        await ApiAssert.FailsWith(HttpStatusCode.NotFound, () => app.Api(keeper).GetCampaignAsync(campaign.Id, Cancellation));
    }

    [Fact]
    public async Task Player_lists_only_own_campaigns_without_rights()
    {
        TestDatabase.SkipIfMissing();
        var keeper = await app.AddUserAsync(UserRole.Keeper);
        var player = await app.AddUserAsync();
        var mine = await app.AddCampaignAsync(keeper, player);
        var foreign = await app.AddCampaignAsync(keeper);

        var list = await app.Api(player).GetCampaignsAsync(Cancellation);

        var row = Assert.Single(list);
        Assert.Equal(mine.Id, row.Id);
        Assert.Equal(CampaignRole.Player, row.MyRole);
        Assert.False(row.CanEdit);
        Assert.DoesNotContain(list, c => c.Id == foreign.Id);
    }

    // Паритет 11: псевдоним в кампании. Хранится, только если отличается от имени профиля.
    [Fact]
    public async Task Join_keeps_alias_only_when_it_differs_from_profile_name()
    {
        TestDatabase.SkipIfMissing();
        var keeper = await app.AddUserAsync(UserRole.Keeper, "Хранитель");
        var dima = await app.AddUserAsync(UserRole.Player, "Дмитрий");
        var anna = await app.AddUserAsync(UserRole.Player, "Анна");
        var campaign = await app.AddCampaignAsync(keeper);

        var afterDima = await app.Api(dima).JoinAsync(campaign.Id, new JoinCampaignRequest("  Дима "), Cancellation);
        var afterAnna = await app.Api(anna).JoinAsync(campaign.Id, new JoinCampaignRequest("Анна"), Cancellation);

        var dimaRow = Assert.Single(afterDima.Members, m => m.UserId == dima.Id);
        Assert.Equal("Дима", dimaRow.Name);
        Assert.Equal("Дима", dimaRow.Alias);
        Assert.True(dimaRow.IsMe);
        Assert.True(afterDima.CanLeave);

        var annaRow = Assert.Single(afterAnna.Members, m => m.UserId == anna.Id);
        Assert.Equal("Анна", annaRow.Name);
        Assert.Null(annaRow.Alias);

        var keeperRow = afterAnna.Members[0];
        Assert.Equal(CampaignRole.Keeper, keeperRow.Role);
        Assert.Equal("Хранитель", keeperRow.Name);
        Assert.False(keeperRow.CanRemove);
    }

    [Fact]
    public async Task Cannot_join_twice_or_completed_campaign()
    {
        TestDatabase.SkipIfMissing();
        var keeper = await app.AddUserAsync(UserRole.Keeper);
        var player = await app.AddUserAsync();
        var campaign = await app.AddCampaignAsync(keeper, player);
        var completed = await app.AddCampaignAsync(keeper, CampaignStatus.Completed, CampaignKind.Campaign);

        await ApiAssert.FailsWith(HttpStatusCode.Forbidden,
            () => app.Api(player).JoinAsync(campaign.Id, new JoinCampaignRequest(null), Cancellation));
        await ApiAssert.FailsWith(HttpStatusCode.Forbidden,
            () => app.Api(player).JoinAsync(completed.Id, new JoinCampaignRequest(null), Cancellation));
    }

    // Псевдоним меняет сам участник и Хранитель кампании; сосед по столу — нет. Пусто — снова имя профиля.
    [Fact]
    public async Task Alias_is_changed_by_member_or_keeper()
    {
        TestDatabase.SkipIfMissing();
        var keeper = await app.AddUserAsync(UserRole.Keeper, "Хранитель");
        var player = await app.AddUserAsync(UserRole.Player, "Дмитрий");
        var neighbour = await app.AddUserAsync(UserRole.Player, "Сосед");
        var campaign = await app.AddCampaignAsync(keeper, player, neighbour);

        var self = await app.Api(player).UpdateMemberAsync(campaign.Id, player.Id, new UpdateMemberRequest("Дима"), Cancellation);
        Assert.Equal("Дима", self.Name);

        var byKeeper = await app.Api(keeper).UpdateMemberAsync(campaign.Id, player.Id, new UpdateMemberRequest("Профессор"), Cancellation);
        Assert.Equal("Профессор", byKeeper.Name);

        await ApiAssert.FailsWith(HttpStatusCode.Forbidden,
            () => app.Api(neighbour).UpdateMemberAsync(campaign.Id, player.Id, new UpdateMemberRequest("Шутка"), Cancellation));
        await ApiAssert.FailsWith(HttpStatusCode.BadRequest,
            () => app.Api(player).UpdateMemberAsync(campaign.Id, player.Id, new UpdateMemberRequest("me@example.test"), Cancellation));

        var reset = await app.Api(player).UpdateMemberAsync(campaign.Id, player.Id, new UpdateMemberRequest(""), Cancellation);
        Assert.Null(reset.Alias);
        Assert.Equal("Дмитрий", reset.Name);
    }

    // Выход и исключение не трогают лист: он остаётся у игрока без кампании (составной FK).
    [Fact]
    public async Task Leaving_keeps_the_sheet_with_the_player()
    {
        TestDatabase.SkipIfMissing();
        var keeper = await app.AddUserAsync(UserRole.Keeper);
        var player = await app.AddUserAsync();
        var kicked = await app.AddUserAsync();
        var campaign = await app.AddCampaignAsync(keeper, player, kicked);
        var sheet = await app.AddCharacterAsync(CharacterKind.Player, "Харви Уолтерс", player.Id, campaign.Id);

        await app.Api(player).RemoveMemberAsync(campaign.Id, player.Id, Cancellation);
        await ApiAssert.FailsWith(HttpStatusCode.Forbidden, () => app.Api(kicked).RemoveMemberAsync(campaign.Id, keeper.Id, Cancellation));
        await app.Api(keeper).RemoveMemberAsync(campaign.Id, kicked.Id, Cancellation);

        await using var db = app.Database.CreateContext();
        var stored = await db.Characters.SingleAsync(c => c.Id == sheet.Id, Cancellation);
        Assert.Equal(player.Id, stored.OwnerId);
        Assert.Null(stored.CampaignId);
        Assert.False(await db.CampaignMembers.AnyAsync(m => m.CampaignId == campaign.Id && m.Role == CampaignRole.Player, Cancellation));
        await ApiAssert.FailsWith(HttpStatusCode.NotFound, () => app.Api(player).GetJournalAsync(campaign.Id, Cancellation));
    }

    [Fact]
    public async Task Keeper_cannot_be_removed()
    {
        TestDatabase.SkipIfMissing();
        var keeper = await app.AddUserAsync(UserRole.Keeper);
        var admin = await app.AddUserAsync(UserRole.Admin);
        var campaign = await app.AddCampaignAsync(keeper);

        await ApiAssert.FailsWith(HttpStatusCode.Conflict, () => app.Api(keeper).RemoveMemberAsync(campaign.Id, keeper.Id, Cancellation));
        await ApiAssert.FailsWith(HttpStatusCode.Conflict, () => app.Api(admin).RemoveMemberAsync(campaign.Id, keeper.Id, Cancellation));
    }

    // Удаление кампании: журнал и прохождения уходят каскадом, лист игрока остаётся у него, НПС кампании —
    // возвращается в библиотеку.
    [Fact]
    public async Task Deleting_campaign_keeps_sheets_and_returns_npcs_to_library()
    {
        TestDatabase.SkipIfMissing();
        var keeper = await app.AddUserAsync(UserRole.Keeper);
        var player = await app.AddUserAsync();
        var campaign = await app.AddCampaignAsync(keeper, player);
        var sheet = await app.AddCharacterAsync(CharacterKind.Player, "Харви", player.Id, campaign.Id);
        var npc = await app.AddCharacterAsync(CharacterKind.Npc, "Джексон Элиас", campaign: campaign.Id);
        await app.Api(keeper).AddSessionAsync(campaign.Id,
            new CampaignSessionInput(new DateOnly(2026, 10, 1), 1, null, "Хроника", null, null, false), Cancellation);

        await app.Api(keeper).DeleteCampaignAsync(campaign.Id, Cancellation);

        await using var db = app.Database.CreateContext();
        Assert.Null((await db.Characters.SingleAsync(c => c.Id == sheet.Id, Cancellation)).CampaignId);
        Assert.Null((await db.Characters.SingleAsync(c => c.Id == npc.Id, Cancellation)).CampaignId);
        Assert.False(await db.CampaignSessions.AnyAsync(s => s.CampaignId == campaign.Id, Cancellation));
    }

    // Почты не светятся нигде: имя, равное почте, приходит пустым.
    [Fact]
    public async Task Emails_never_leave_the_server()
    {
        TestDatabase.SkipIfMissing();
        var keeper = await app.AddUserAsync(UserRole.Keeper);
        var player = await app.AddUserAsync();
        var newcomer = await app.AddUserAsync();
        var campaign = await app.AddCampaignAsync(keeper, player);

        var details = await app.Http(player).GetStringAsync(Contracts.Campaigns.CampaignsRoutes.Campaign(campaign.Id), Cancellation);
        var home = await app.Http(newcomer).GetStringAsync(Contracts.Campaigns.CampaignsRoutes.Home, Cancellation);

        Assert.DoesNotContain("@", details);
        Assert.DoesNotContain("@", home);
        Assert.Contains(campaign.Id.ToString(), home);
    }

    // Главная гостю кампаний не показывает (v1 отдавал анониму названия и почты Хранителей).
    [Fact]
    public async Task Anonymous_gets_nothing()
    {
        TestDatabase.SkipIfMissing();
        await ApiAssert.FailsWith(HttpStatusCode.Unauthorized, () => app.Api(null).GetHomeAsync(Cancellation));
        await ApiAssert.FailsWith(HttpStatusCode.Unauthorized, () => app.Api(null).GetCampaignsAsync(Cancellation));
    }

    // C1: живая игра — первой: активные, на паузе, планируемые, завершённые; внутри — новые сверху.
    [Fact]
    public async Task List_puts_active_campaigns_first_and_completed_last()
    {
        TestDatabase.SkipIfMissing();
        var keeper = await app.AddUserAsync(UserRole.Keeper);
        var player = await app.AddUserAsync();
        var done = await app.AddCampaignAsync(keeper, CampaignStatus.Completed, CampaignKind.Campaign, player);
        var planned = await app.AddCampaignAsync(keeper, CampaignStatus.Planning, CampaignKind.Campaign, player);
        var active = await app.AddCampaignAsync(keeper, CampaignStatus.Active, CampaignKind.Campaign, player);
        var paused = await app.AddCampaignAsync(keeper, CampaignStatus.OnHold, CampaignKind.Campaign, player);
        var doneLater = await app.AddCampaignAsync(keeper, CampaignStatus.Completed, CampaignKind.Campaign, player);

        var ids = (await app.Api(player).GetCampaignsAsync(Cancellation)).Select(c => c.Id).ToList();

        Assert.Equal([active.Id, paused.Id, planned.Id, doneLater.Id, done.Id], ids);
    }

    // Обработчик общий (Platform), а не кампаний: то же в админке — неверная роль в теле.
    [Fact]
    public async Task Broken_enum_in_another_module_is_400_too()
    {
        TestDatabase.SkipIfMissing();
        var admin = await app.AddUserAsync(UserRole.Admin);
        var someone = await app.AddUserAsync();

        using var request = new HttpRequestMessage(HttpMethod.Put, Contracts.Admin.AdminRoutes.UserRole(someone.Id))
        {
            Content = new StringContent("""{"role":"Bogus"}""", System.Text.Encoding.UTF8, "application/json"),
        };
        using var response = await app.Http(admin).SendAsync(request, Cancellation);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // #194: неверное значение enum (и любая ошибка чтения тела) — 400 с понятным текстом, а не 500; общим обработчиком,
    // поэтому и создание, и правка, и журнал.
    [Theory]
    [InlineData("POST", "", """{"name":"Маски","kind":"Campaign","status":"Planning","era":"Bogus"}""", "era")]
    [InlineData("PUT", "{id}", """{"name":"Маски","kind":"Campaign","status":"НеТакой","era":"Classic"}""", "status")]
    [InlineData("POST", "", "{not json", null)]
    public async Task Broken_body_is_400_with_text_not_500(string method, string path, string body, string? field)
    {
        TestDatabase.SkipIfMissing();
        var keeper = await app.AddUserAsync(UserRole.Keeper);
        var campaign = await app.AddCampaignAsync(keeper);
        var url = Contracts.Campaigns.CampaignsRoutes.Campaigns + (path.Length == 0 ? "" : "/" + campaign.Id);

        using var request = new HttpRequestMessage(new HttpMethod(method), url)
        {
            Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json"),
        };
        using var response = await app.Http(keeper).SendAsync(request, Cancellation);
        var text = await response.Content.ReadAsStringAsync(Cancellation);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var problem = System.Text.Json.JsonDocument.Parse(text);
        var detail = problem.RootElement.GetProperty("detail").GetString()!;
        Assert.Equal("invalid", problem.RootElement.GetProperty("code").GetString());
        Assert.Contains(field is null ? "корректный JSON" : $"«{field}»", detail, StringComparison.Ordinal);
        Assert.DoesNotContain("System.", text, StringComparison.Ordinal);
    }
}
