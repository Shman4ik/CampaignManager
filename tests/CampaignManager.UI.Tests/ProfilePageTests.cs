using System.Text.Json;
using Bunit;
using CampaignManager.Contracts.Profile;
using CampaignManager.Core.Identity;
using CampaignManager.UI.Identity;
using CampaignManager.UI.Profile;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CampaignManager.UI.Tests;

// Кабинет: галочка «заменить и имена в кампаниях» выключена по умолчанию и есть, только когда есть что
// заменять; после сохранения имени меню перечитывает /me (без перезагрузки приложения, как в v1).
public sealed class ProfilePageTests : KitContext
{
    private readonly FakeProfileApi _api = new();
    private readonly FakeSession _session = new();

    public ProfilePageTests()
    {
        Services.AddSingleton<IProfileApi>(_api);
        Services.AddSingleton<IUserSession>(_session);
    }

    [Fact]
    public void Replace_aliases_is_off_by_default()
    {
        _api.Profile = Profile(aliasCount: 2);

        var cut = Render<ProfilePage>();

        var checkbox = cut.WaitForElement("[data-testid='replace-aliases']");
        Assert.False(checkbox.HasAttribute("checked"));
        Assert.Contains("Применить ко всем кампаниям", cut.Markup);
    }

    [Fact]
    public void No_replace_checkbox_without_aliases()
    {
        _api.Profile = Profile(aliasCount: 0);

        var cut = Render<ProfilePage>();

        cut.WaitForElement("[data-testid='profile-name']");
        Assert.Empty(cut.FindAll("[data-testid='replace-aliases']"));
    }

    [Fact]
    public void Saving_name_sends_flag_and_refreshes_session()
    {
        _api.Profile = Profile(aliasCount: 1);
        var cut = Render<ProfilePage>();
        cut.WaitForElement("[data-testid='replace-aliases']");

        cut.Find("input.cm-input").Input("Дмитрий Петров");
        cut.Find("[data-testid='replace-aliases']").Change(true);
        cut.Find("form").Submit();

        cut.WaitForAssertion(() => Assert.Equal(new UpdateDisplayNameRequest("Дмитрий Петров", true), _api.LastRename));
        Assert.Equal(1, _session.Refreshes);
        // Имя — только полем, заголовка-дубля нет (PR4).
        cut.WaitForAssertion(() => Assert.Equal("Дмитрий Петров", cut.Find("[data-testid='profile-name']").GetAttribute("value")));
        Assert.Empty(cut.FindAll("div[data-testid='profile-name']"));
    }

    [Fact]
    public void Keeper_sees_no_application_form()
    {
        _api.Profile = Profile(aliasCount: 0) with { Role = UserRole.Keeper, CanApply = false };

        var cut = Render<ProfilePage>();

        cut.WaitForElement("[data-testid='profile-name']");
        Assert.DoesNotContain("Подать заявку", cut.Markup);
    }

    private static ProfileDto Profile(int aliasCount) =>
        new(Guid.NewGuid(), "dima@example.test", "Дмитрий", UserRole.Player, 2, 1, aliasCount, null, CanApply: true);

    private sealed class FakeProfileApi : IProfileApi
    {
        public ProfileDto Profile { get; set; } = null!;

        public UpdateDisplayNameRequest? LastRename { get; private set; }

        public Task<ProfileDto> GetProfileAsync(CancellationToken cancellationToken = default) => Task.FromResult(Profile);

        public Task<ProfileDto> UpdateDisplayNameAsync(UpdateDisplayNameRequest request, CancellationToken cancellationToken = default)
        {
            LastRename = request;
            Profile = Profile with { DisplayName = request.DisplayName.Trim(), AliasCount = request.ReplaceAliases ? 0 : Profile.AliasCount };
            return Task.FromResult(Profile);
        }

        public Task<ProfileDto> SubmitKeeperApplicationAsync(SubmitKeeperApplicationRequest request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public SubmitKeeperApplicationRequest? LastEdit { get; private set; }

        public int Withdrawals { get; private set; }

        public Task<ProfileDto> UpdateKeeperApplicationAsync(SubmitKeeperApplicationRequest request, CancellationToken cancellationToken = default)
        {
            LastEdit = request;
            Profile = Profile with { LatestApplication = Profile.LatestApplication! with { Message = request.Message?.Trim() ?? "" } };
            return Task.FromResult(Profile);
        }

        public Task<ProfileDto> WithdrawKeeperApplicationAsync(CancellationToken cancellationToken = default)
        {
            Withdrawals++;
            Profile = Profile with { LatestApplication = null, CanApply = true };
            return Task.FromResult(Profile);
        }

        public Task<PreferencesDto> GetPreferencesAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new PreferencesDto(new Dictionary<string, JsonElement>()));

        public Task SetPreferenceAsync(string key, JsonElement value, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task RemovePreferenceAsync(string key, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeSession : IUserSession
    {
        public int Refreshes { get; private set; }

        public Task RefreshAsync()
        {
            Refreshes++;
            return Task.CompletedTask;
        }
    }
}
