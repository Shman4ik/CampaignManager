using System.Net;
using System.Net.Http.Json;
using System.Text;
using CampaignManager.ApiClient.Characters;
using CampaignManager.Contracts.Characters;
using CampaignManager.Contracts.Platform;
using CampaignManager.Core;
using CampaignManager.Core.Catalogs;
using CampaignManager.Core.Characters;
using CampaignManager.Core.Documents;
using CampaignManager.Core.Identity;
using CampaignManager.Data.Catalogs;
using CampaignManager.Data.Files;
using CampaignManager.Data.Identity;
using CampaignManager.Server.Tests.Campaigns;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CampaignManager.Server.Tests.Characters;

/// <summary>
/// Лист сыщика через API (T2.3): права по таблице Access, запись с <c>If-Match</c> и конфликт двух устройств,
/// игрок — из владельца, а не из документа, соседи по столу и сыщики кампании для Хранителя.
/// </summary>
public sealed class CharactersApiTests(CampaignsApp app) : IClassFixture<CampaignsApp>
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private CharactersApiClient Api(User user) => new(app.Http(user));

    private static async Task<ApiException> Fails(HttpStatusCode status, Func<Task> call)
    {
        var error = await Assert.ThrowsAsync<ApiException>(call);
        Assert.Equal(status, error.StatusCode);
        return error;
    }

    /// <summary>Хранитель, игрок с листом в кампании (псевдоним «Аня»), второй игрок кампании и посторонний.</summary>
    private async Task<(User Keeper, User Player, User Neighbour, User Stranger, Guid CampaignId, Guid SheetId)> TableAsync()
    {
        var keeper = await app.AddUserAsync(UserRole.Keeper, "Хранитель");
        var player = await app.AddUserAsync(UserRole.Player);
        var neighbour = await app.AddUserAsync(UserRole.Player, "Илья");
        var stranger = await app.AddUserAsync(UserRole.Player, "Чужой");
        var campaign = await app.AddCampaignAsync(keeper, player, neighbour);
        await using (var db = app.Database.CreateContext())
        {
            var member = await db.CampaignMembers.SingleAsync(m => m.CampaignId == campaign.Id && m.UserId == player.Id, Cancellation);
            member.DisplayName = "Аня";
            await db.SaveChangesAsync(Cancellation);
        }

        var sheet = await app.AddCharacterAsync(CharacterKind.Player, "Харви Уолтерс", player.Id, campaign.Id);
        await app.AddCharacterAsync(CharacterKind.Player, "Нора Флинн", neighbour.Id, campaign.Id);
        return (keeper, player, neighbour, stranger, campaign.Id, sheet.Id);
    }

    private async Task<Skill> AddSkillAsync(string name, int baseValue = 20)
    {
        await using var db = app.Database.CreateContext();
        var skill = new Skill { Name = $"{name} {Guid.NewGuid():N}"[..30], BaseValue = baseValue, Category = SkillCategory.InformationGathering };
        db.Skills.Add(skill);
        await db.SaveChangesAsync(Cancellation);
        return skill;
    }

    [Fact]
    public async Task Owner_and_campaign_keeper_read_the_sheet_and_others_get_404()
    {
        TestDatabase.SkipIfMissing();
        var (keeper, player, neighbour, stranger, campaignId, sheetId) = await TableAsync();

        var mine = await Api(player).GetAsync(sheetId, Cancellation);
        Assert.Equal("Харви Уолтерс", mine.Sheet.Personal.Name);
        Assert.Equal("Аня", mine.PlayerName);
        Assert.Equal(campaignId, mine.CampaignId);
        Assert.Equal(Era.Classic, mine.Era);
        Assert.True(mine.CanEdit);
        Assert.True(mine.IsMine); // нового сыщика вместо выбывшего заводит игрок
        Assert.NotEqual(0u, mine.Version);

        var keepers = await Api(keeper).GetAsync(sheetId, Cancellation);
        Assert.True(keepers.CanEdit);
        Assert.False(keepers.IsMine);

        // «Нет листа» и «чужой лист» неразличимы — и для соседа по столу тоже
        await Fails(HttpStatusCode.NotFound, () => Api(neighbour).GetAsync(sheetId, Cancellation));
        await Fails(HttpStatusCode.NotFound, () => Api(stranger).GetAsync(sheetId, Cancellation));
        await Fails(HttpStatusCode.NotFound, () => Api(stranger).GetAsync(Guid.NewGuid(), Cancellation));
    }

    [Fact]
    public async Task Get_returns_etag_with_version()
    {
        TestDatabase.SkipIfMissing();
        var (_, player, _, _, _, sheetId) = await TableAsync();

        using var response = await app.Http(player).GetAsync(CharactersRoutes.Character(sheetId), Cancellation);
        var dto = await response.Content.ReadFromJsonAsync(Contracts.ContractsJsonContext.Default.CharacterDto, Cancellation);

        Assert.Equal($"\"{dto!.Version}\"", response.Headers.ETag?.Tag);
        Assert.True(response.Headers.CacheControl?.NoStore);
    }

    // Ошибка v1 6: «Сохранить» перезаписывало имя игрока именем слота. В 2.0 игрок — колонка строки.
    [Fact]
    public async Task Save_writes_the_document_and_moves_the_version_without_touching_the_owner()
    {
        TestDatabase.SkipIfMissing();
        var (keeper, player, _, _, _, sheetId) = await TableAsync();
        var hearing = await AddSkillAsync("Слух");
        var api = Api(player);
        var read = await api.GetAsync(sheetId, Cancellation);

        read.Sheet.Personal.Name = "Харви «Док» Уолтерс";
        read.Sheet.Current.Luck = 45;
        read.Sheet.Skills.Add(new SheetSkill { SkillId = hearing.Id, Value = 35, Checked = true });
        read.Sheet.Biography.Notes = "Боится подвалов";
        var saved = await api.SaveSheetAsync(sheetId, read.Sheet, read.Version, Cancellation);

        Assert.NotEqual(read.Version, saved.Version);
        var again = await Api(keeper).GetAsync(sheetId, Cancellation);
        Assert.Equal(saved.Version, again.Version);
        Assert.Equal("Харви «Док» Уолтерс", again.Sheet.Personal.Name);
        Assert.Equal(45, again.Sheet.Current.Luck);
        Assert.Equal(35, again.Sheet.Skills.Single().Value);
        Assert.Equal("Боится подвалов", again.Sheet.Biography.Notes);
        Assert.Equal("Аня", again.PlayerName);

        await using var db = app.Database.CreateContext();
        var row = await db.Characters.SingleAsync(c => c.Id == sheetId, Cancellation);
        Assert.Equal(player.Id, row.OwnerId);
        Assert.Equal("Харви «Док» Уолтерс", row.Name); // generated-колонка идёт за документом
    }

    // Главный случай карточки: правка одного листа с двух устройств — второму 409, а не молчаливая перезапись.
    [Fact]
    public async Task Second_device_with_stale_version_gets_conflict()
    {
        TestDatabase.SkipIfMissing();
        var (keeper, player, _, _, _, sheetId) = await TableAsync();
        var tablet = await Api(keeper).GetAsync(sheetId, Cancellation);
        var phone = await Api(player).GetAsync(sheetId, Cancellation);

        tablet.Sheet.Current.HitPoints = 3;
        await Api(keeper).SaveSheetAsync(sheetId, tablet.Sheet, tablet.Version, Cancellation);

        phone.Sheet.Current.Luck = 10;
        var conflict = await Fails(HttpStatusCode.Conflict, () => Api(player).SaveSheetAsync(sheetId, phone.Sheet, phone.Version, Cancellation));
        Assert.True(conflict.IsStale);

        var stored = await Api(player).GetAsync(sheetId, Cancellation);
        Assert.Equal(3, stored.Sheet.Current.HitPoints);
        Assert.NotEqual(10, stored.Sheet.Current.Luck);
    }

    [Fact]
    public async Task Save_without_if_match_is_428_and_unknown_skill_is_400()
    {
        TestDatabase.SkipIfMissing();
        var (_, player, _, _, _, sheetId) = await TableAsync();
        var read = await Api(player).GetAsync(sheetId, Cancellation);

        using var response = await app.Http(player).PutAsJsonAsync(CharactersRoutes.Sheet(sheetId), read.Sheet,
            Contracts.ContractsJsonContext.Default.CharacterSheet, Cancellation);
        Assert.Equal(HttpStatusCode.PreconditionRequired, response.StatusCode);

        read.Sheet.Skills.Add(new SheetSkill { SkillId = Guid.NewGuid(), Value = 50 });
        await Fails(HttpStatusCode.BadRequest, () => Api(player).SaveSheetAsync(sheetId, read.Sheet, read.Version, Cancellation));
    }

    // Старое приложение не стирает то, чего не знает: незнакомые поля документа переживают запись и чтение.
    [Fact]
    public async Task Unknown_fields_of_the_document_survive_save()
    {
        TestDatabase.SkipIfMissing();
        var (_, player, _, _, _, sheetId) = await TableAsync();
        var read = await Api(player).GetAsync(sheetId, Cancellation);

        var json = """{"personal":{"name":"Харви","nickname":"Док"},"futureBlock":{"x":1}}""";
        using var request = new HttpRequestMessage(HttpMethod.Put, CharactersRoutes.Sheet(sheetId))
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };
        request.Headers.IfMatch.Add(new System.Net.Http.Headers.EntityTagHeaderValue($"\"{read.Version}\""));
        using var response = await app.Http(player).SendAsync(request, Cancellation);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        await using var db = app.Database.CreateContext();
        var row = await db.Characters.SingleAsync(c => c.Id == sheetId, Cancellation);
        var stored = CmJson.ReadSheet(row.Sheet, row.SheetVersion);
        Assert.Equal("Док", stored.Personal.Extra!["nickname"].GetString());
        Assert.True(stored.Extra!.ContainsKey("futureBlock"));
    }

    [Fact]
    public async Task Pregen_is_read_only_for_a_player()
    {
        TestDatabase.SkipIfMissing();
        var player = await app.AddUserAsync(UserRole.Player);
        var pregen = await app.AddCharacterAsync(CharacterKind.Pregen, "Преген");
        var read = await Api(player).GetAsync(pregen.Id, Cancellation);

        Assert.False(read.CanEdit);
        Assert.Null(read.PlayerName);
        await Fails(HttpStatusCode.Forbidden, () => Api(player).SaveSheetAsync(pregen.Id, read.Sheet, read.Version, Cancellation));
    }

    [Fact]
    public async Task Party_lists_neighbours_by_campaign_without_emails()
    {
        TestDatabase.SkipIfMissing();
        var (_, player, _, _, _, sheetId) = await TableAsync();

        var party = await Api(player).GetPartyAsync(sheetId, Cancellation);

        var nora = Assert.Single(party);
        Assert.Equal("Нора Флинн", nora.Name);
        Assert.Equal("Антиквар", nora.Occupation);
        Assert.Equal("Илья", nora.PlayerName);
    }

    [Fact]
    public async Task Campaign_investigators_are_for_the_keeper_only()
    {
        TestDatabase.SkipIfMissing();
        var (keeper, player, _, _, campaignId, _) = await TableAsync();

        var investigators = await Api(keeper).GetCampaignInvestigatorsAsync(campaignId, Cancellation);

        Assert.Equal(["Нора Флинн", "Харви Уолтерс"], investigators.Select(i => i.Name));
        Assert.Contains(investigators, i => i.PlayerName == "Аня");
        await Fails(HttpStatusCode.Forbidden, () => Api(player).GetCampaignInvestigatorsAsync(campaignId, Cancellation));
    }

    [Fact]
    public async Task Second_active_sheet_in_campaign_is_a_conflict()
    {
        TestDatabase.SkipIfMissing();
        var (_, player, _, _, campaignId, sheetId) = await TableAsync();
        var retired = await app.AddCharacterAsync(CharacterKind.Player, "Старый", player.Id, campaignId, status: CharacterStatus.Retired);
        var read = await Api(player).GetAsync(retired.Id, Cancellation);

        var error = await Fails(HttpStatusCode.Conflict, () => Api(player).SetStatusAsync(retired.Id, CharacterStatus.Active, read.Version, Cancellation));
        Assert.Contains("активный лист", error.Message, StringComparison.Ordinal);

        var current = await Api(player).GetAsync(sheetId, Cancellation);
        var saved = await Api(player).SetStatusAsync(sheetId, CharacterStatus.Inactive, current.Version, Cancellation);
        Assert.NotEqual(current.Version, saved.Version);
        Assert.Equal(CharacterStatus.Inactive, (await Api(player).GetAsync(sheetId, Cancellation)).Status);
    }

    [Fact]
    public async Task Portrait_is_a_file_reference()
    {
        TestDatabase.SkipIfMissing();
        var (_, player, _, _, _, sheetId) = await TableAsync();
        Guid fileId;
        await using (var db = app.Database.CreateContext())
        {
            var file = new StoredFile { ExternalUrl = $"https://example.test/{Guid.NewGuid():N}.png", ContentType = "image/png" };
            db.Files.Add(file);
            await db.SaveChangesAsync(Cancellation);
            fileId = file.Id;
        }

        var read = await Api(player).GetAsync(sheetId, Cancellation);
        await Fails(HttpStatusCode.BadRequest, () => Api(player).SetPortraitAsync(sheetId, Guid.NewGuid(), read.Version, Cancellation));
        await Api(player).SetPortraitAsync(sheetId, fileId, read.Version, Cancellation);

        var withPortrait = await Api(player).GetAsync(sheetId, Cancellation);
        Assert.Equal(fileId, withPortrait.PortraitFileId);
        Assert.EndsWith(fileId.ToString(), withPortrait.PortraitUrl, StringComparison.Ordinal);
    }
}
