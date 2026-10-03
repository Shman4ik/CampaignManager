using System.Net;
using System.Text.Json;
using CampaignManager.ApiClient.Profile;
using CampaignManager.Contracts.Profile;
using CampaignManager.Core.Characters;
using CampaignManager.Core.Identity;
using CampaignManager.Data.Identity;
using CampaignManager.Server.Tests.Campaigns;
using Xunit;

namespace CampaignManager.Server.Tests.Profile;

/// <summary>Личный кабинет через API: имя и псевдонимы, заявка на Хранителя, настройки, только свои данные.</summary>
public sealed class ProfileApiTests(CampaignsApp app) : IClassFixture<CampaignsApp>
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private ProfileApiClient Api(User user) => new(app.Http(user));

    [Fact]
    public async Task Profile_counts_my_campaigns_investigators_and_aliases()
    {
        TestDatabase.SkipIfMissing();
        var keeper = await app.AddUserAsync(UserRole.Keeper, "Хранитель");
        var player = await app.AddUserAsync(UserRole.Player, "Дмитрий");
        var first = await app.AddCampaignAsync(keeper, player);
        await app.AddCampaignAsync(keeper, player);
        await app.Api(player).UpdateMemberAsync(first.Id, player.Id, new("Дима"), Cancellation);
        await app.AddCharacterAsync(CharacterKind.Player, "Харви", owner: player.Id, campaign: first.Id);
        await app.AddCharacterAsync(CharacterKind.Player, "Архив", owner: player.Id, status: CharacterStatus.Archived);

        var profile = await Api(player).GetProfileAsync(Cancellation);

        Assert.Equal(player.Id, profile.Id);
        Assert.Equal("Дмитрий", profile.DisplayName);
        Assert.Equal(player.Email, profile.Email);
        Assert.Equal(UserRole.Player, profile.Role);
        Assert.Equal(2, profile.CampaignCount);
        Assert.Equal(1, profile.CharacterCount);
        Assert.Equal(1, profile.AliasCount);
        Assert.Null(profile.LatestApplication);
        Assert.True(profile.CanApply);
    }

    // Имена в кампаниях нарочно разные — по умолчанию смена имени их не трогает (знание v1).
    [Fact]
    public async Task Rename_keeps_campaign_aliases_unless_asked()
    {
        TestDatabase.SkipIfMissing();
        var keeper = await app.AddUserAsync(UserRole.Keeper, "Хранитель");
        var player = await app.AddUserAsync(UserRole.Player, "Дмитрий");
        var aliased = await app.AddCampaignAsync(keeper, player);
        var plain = await app.AddCampaignAsync(keeper, player);
        await app.Api(player).UpdateMemberAsync(aliased.Id, player.Id, new("Дима"), Cancellation);

        var renamed = await Api(player).UpdateDisplayNameAsync(new UpdateDisplayNameRequest("  Дмитрий Петров  "), Cancellation);

        Assert.Equal("Дмитрий Петров", renamed.DisplayName);
        Assert.Equal(1, renamed.AliasCount);
        Assert.Equal("Дима", await NameIn(aliased.Id, player));
        Assert.Equal("Дмитрий Петров", await NameIn(plain.Id, player));

        var me = await new ApiClient.Identity.IdentityApiClient(app.Http(player)).GetMeAsync(Cancellation);
        Assert.Equal("Дмитрий Петров", me!.DisplayName);
    }

    [Fact]
    public async Task Rename_with_replace_resets_aliases_in_every_campaign()
    {
        TestDatabase.SkipIfMissing();
        var keeper = await app.AddUserAsync(UserRole.Keeper, "Хранитель");
        var player = await app.AddUserAsync(UserRole.Player, "Дмитрий");
        var other = await app.AddUserAsync(UserRole.Player, "Соня");
        var first = await app.AddCampaignAsync(keeper, player, other);
        var second = await app.AddCampaignAsync(keeper, player);
        await app.Api(player).UpdateMemberAsync(first.Id, player.Id, new("Дима"), Cancellation);
        await app.Api(player).UpdateMemberAsync(second.Id, player.Id, new("Митя"), Cancellation);
        await app.Api(other).UpdateMemberAsync(first.Id, other.Id, new("Сонечка"), Cancellation);

        var renamed = await Api(player).UpdateDisplayNameAsync(new UpdateDisplayNameRequest("Дмитрий Петров", ReplaceAliases: true),
            Cancellation);

        Assert.Equal(0, renamed.AliasCount);
        Assert.Equal("Дмитрий Петров", await NameIn(first.Id, player));
        Assert.Equal("Дмитрий Петров", await NameIn(second.Id, player));
        // Чужие псевдонимы не тронуты.
        Assert.Equal("Сонечка", await NameIn(first.Id, other));
    }

    [Theory]
    [InlineData("   ", "пустым")]
    [InlineData("dima@example.test", "@")]
    [InlineData("Очень длинное имя, которое никак не помещается в шестьдесят четыре символа отображаемого имени", "64")]
    public async Task Bad_name_is_rejected_with_text(string name, string expected)
    {
        TestDatabase.SkipIfMissing();
        var player = await app.AddUserAsync(UserRole.Player, "Дмитрий");

        var message = await ApiAssert.FailsWith(HttpStatusCode.BadRequest,
            () => Api(player).UpdateDisplayNameAsync(new UpdateDisplayNameRequest(name), Cancellation));

        Assert.Contains(expected, message);
        Assert.Equal("Дмитрий", (await Api(player).GetProfileAsync(Cancellation)).DisplayName);
    }

    // UX-2 X1: пока заявку не рассмотрели, игрок дописывает текст или отзывает её; рассмотренную - нельзя.
    [Fact]
    public async Task Player_edits_and_withdraws_pending_application_and_can_apply_again()
    {
        TestDatabase.SkipIfMissing();
        var player = await app.AddUserAsync(UserRole.Player, "Соискатель");
        var other = await app.AddUserAsync(UserRole.Player, "Другой");
        await Api(player).SubmitKeeperApplicationAsync(new SubmitKeeperApplicationRequest("Хочу вести"), Cancellation);
        await Api(other).SubmitKeeperApplicationAsync(new SubmitKeeperApplicationRequest("Чужая"), Cancellation);

        var edited = await Api(player).UpdateKeeperApplicationAsync(new SubmitKeeperApplicationRequest("  Хочу вести «Маски»  "), Cancellation);

        var application = Assert.IsType<MyKeeperApplicationDto>(edited.LatestApplication);
        Assert.Equal(KeeperApplicationStatus.Pending, application.Status);
        Assert.Equal("Хочу вести «Маски»", application.Message);
        Assert.Equal("Чужая", (await Api(other).GetProfileAsync(Cancellation)).LatestApplication!.Message);

        var withdrawn = await Api(player).WithdrawKeeperApplicationAsync(Cancellation);

        Assert.Null(withdrawn.LatestApplication);
        Assert.True(withdrawn.CanApply);
        Assert.NotNull((await Api(other).GetProfileAsync(Cancellation)).LatestApplication);
        await ApiAssert.FailsWith(HttpStatusCode.Conflict, () => Api(player).WithdrawKeeperApplicationAsync(Cancellation));
        await ApiAssert.FailsWith(HttpStatusCode.Conflict,
            () => Api(player).UpdateKeeperApplicationAsync(new SubmitKeeperApplicationRequest("ещё"), Cancellation));
        Assert.NotNull((await Api(player).SubmitKeeperApplicationAsync(new SubmitKeeperApplicationRequest("Снова"), Cancellation)).LatestApplication);
    }

    [Fact]
    public async Task Reviewed_application_cannot_be_edited_or_withdrawn_and_text_is_limited()
    {
        TestDatabase.SkipIfMissing();
        var admin = await app.AddUserAsync(UserRole.Admin);
        var player = await app.AddUserAsync(UserRole.Player);
        var rejected = await app.AddUserAsync(UserRole.Player);
        var submitted = await Api(player).SubmitKeeperApplicationAsync(new SubmitKeeperApplicationRequest("Хочу"), Cancellation);
        var second = await Api(rejected).SubmitKeeperApplicationAsync(new SubmitKeeperApplicationRequest("Хочу"), Cancellation);
        var adminApi = new CampaignManager.ApiClient.Admin.AdminApiClient(app.Http(admin));
        await adminApi.ApproveAsync(submitted.LatestApplication!.Id, Cancellation);
        await adminApi.RejectAsync(second.LatestApplication!.Id, "Позже", Cancellation);

        foreach (var user in new[] { player, rejected })
        {
            await ApiAssert.FailsWith(HttpStatusCode.Conflict,
                () => Api(user).UpdateKeeperApplicationAsync(new SubmitKeeperApplicationRequest("Поздно"), Cancellation));
            await ApiAssert.FailsWith(HttpStatusCode.Conflict, () => Api(user).WithdrawKeeperApplicationAsync(Cancellation));
        }

        // Отклонённая остаётся в истории: её комментарий игрок видит.
        Assert.Equal("Позже", (await Api(rejected).GetProfileAsync(Cancellation)).LatestApplication!.ReviewComment);

        var pending = await app.AddUserAsync(UserRole.Player);
        await Api(pending).SubmitKeeperApplicationAsync(new SubmitKeeperApplicationRequest("Хочу"), Cancellation);
        await ApiAssert.FailsWith(HttpStatusCode.BadRequest, () => Api(pending).UpdateKeeperApplicationAsync(
            new SubmitKeeperApplicationRequest(new string('а', ProfileLimits.ApplicationMessageLength + 1)), Cancellation));
    }

    [Fact]
    public async Task Anonymous_cannot_edit_or_withdraw_application()
    {
        TestDatabase.SkipIfMissing();
        var anonymous = new ProfileApiClient(app.CreateClient());

        await ApiAssert.FailsWith(HttpStatusCode.Unauthorized, () => anonymous.WithdrawKeeperApplicationAsync(Cancellation));
        await ApiAssert.FailsWith(HttpStatusCode.Unauthorized,
            () => anonymous.UpdateKeeperApplicationAsync(new SubmitKeeperApplicationRequest("x"), Cancellation));
    }

    [Fact]
    public async Task Player_applies_once_while_pending()
    {
        TestDatabase.SkipIfMissing();
        var player = await app.AddUserAsync(UserRole.Player, "Соискатель");

        var profile = await Api(player).SubmitKeeperApplicationAsync(new SubmitKeeperApplicationRequest("  Хочу вести «Маски»  "), Cancellation);

        var application = Assert.IsType<MyKeeperApplicationDto>(profile.LatestApplication);
        Assert.Equal(KeeperApplicationStatus.Pending, application.Status);
        Assert.Equal("Хочу вести «Маски»", application.Message);
        Assert.False(profile.CanApply);
        Assert.Equal(UserRole.Player, profile.Role);

        var message = await ApiAssert.FailsWith(HttpStatusCode.Conflict,
            () => Api(player).SubmitKeeperApplicationAsync(new SubmitKeeperApplicationRequest(null), Cancellation));
        Assert.Contains("ждёт", message);
    }

    [Fact]
    public async Task Keeper_does_not_apply()
    {
        TestDatabase.SkipIfMissing();
        var keeper = await app.AddUserAsync(UserRole.Keeper);

        await ApiAssert.FailsWith(HttpStatusCode.Conflict,
            () => Api(keeper).SubmitKeeperApplicationAsync(new SubmitKeeperApplicationRequest(null), Cancellation));
        Assert.False((await Api(keeper).GetProfileAsync(Cancellation)).CanApply);
    }

    [Fact]
    public async Task Long_application_message_is_rejected()
    {
        TestDatabase.SkipIfMissing();
        var player = await app.AddUserAsync();

        await ApiAssert.FailsWith(HttpStatusCode.BadRequest,
            () => Api(player).SubmitKeeperApplicationAsync(
                new SubmitKeeperApplicationRequest(new string('а', ProfileLimits.ApplicationMessageLength + 1)), Cancellation));
    }

    // Строка на ключ: запись одного ключа не трогает другие, чужие настройки не видны.
    [Fact]
    public async Task Preferences_are_per_user_and_per_key()
    {
        TestDatabase.SkipIfMissing();
        var player = await app.AddUserAsync();
        var other = await app.AddUserAsync();

        await Api(player).SetPreferenceAsync(PreferenceKeys.SyncLastCharacter, Json("false"), Cancellation);
        await Api(player).SetPreferenceAsync(PreferenceKeys.MusicPinnedTags, Json("""["бой","погоня"]"""), Cancellation);
        await Api(player).SetPreferenceAsync(PreferenceKeys.SyncLastCharacter, Json("true"), Cancellation);
        await Api(other).SetPreferenceAsync(PreferenceKeys.SyncLastCharacter, Json("false"), Cancellation);

        var mine = (await Api(player).GetPreferencesAsync(Cancellation)).Values;
        Assert.Equal(2, mine.Count);
        Assert.True(mine[PreferenceKeys.SyncLastCharacter].GetBoolean());
        Assert.Equal(["бой", "погоня"], mine[PreferenceKeys.MusicPinnedTags].EnumerateArray().Select(e => e.GetString()));

        await Api(player).RemovePreferenceAsync(PreferenceKeys.MusicPinnedTags, Cancellation);
        Assert.Equal([PreferenceKeys.SyncLastCharacter], (await Api(player).GetPreferencesAsync(Cancellation)).Values.Keys);
        Assert.False((await Api(other).GetPreferencesAsync(Cancellation)).Values[PreferenceKeys.SyncLastCharacter].GetBoolean());
    }

    [Theory]
    [InlineData("1bad")]
    [InlineData("ui sync")]
    [InlineData("ключ")]
    public async Task Bad_preference_key_is_rejected(string key)
    {
        TestDatabase.SkipIfMissing();
        var player = await app.AddUserAsync();

        await ApiAssert.FailsWith(HttpStatusCode.BadRequest, () => Api(player).SetPreferenceAsync(key, Json("true"), Cancellation));
    }

    [Fact]
    public async Task Anonymous_gets_401()
    {
        TestDatabase.SkipIfMissing();
        var anonymous = new ProfileApiClient(app.CreateClient());

        await ApiAssert.FailsWith(HttpStatusCode.Unauthorized, () => anonymous.GetProfileAsync(Cancellation));
        await ApiAssert.FailsWith(HttpStatusCode.Unauthorized,
            () => anonymous.SubmitKeeperApplicationAsync(new SubmitKeeperApplicationRequest(null), Cancellation));
    }

    private async Task<string?> NameIn(Guid campaignId, User user)
    {
        var details = await app.Api(user).GetCampaignAsync(campaignId, Cancellation);
        return details.Members.Single(m => m.UserId == user.Id).Name;
    }

    private static JsonElement Json(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }
}
