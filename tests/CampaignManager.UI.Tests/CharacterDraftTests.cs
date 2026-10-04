using System.Net;
using Bunit;
using CampaignManager.Contracts.Campaigns;
using CampaignManager.Contracts.Catalogs;
using CampaignManager.Contracts.Characters;
using CampaignManager.Contracts.Platform;
using CampaignManager.Core.Campaigns;
using CampaignManager.Core.Characters;
using CampaignManager.Core.Documents;
using CampaignManager.UI.Campaigns;
using CampaignManager.UI.Characters.Creation;
using CampaignManager.UI.Platform;
using CampaignManager.UI.Shared;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CampaignManager.UI.Tests;

/// <summary>
/// Черновик помощника на сервере: какой черновик брать при открытии (здешний или серверный), запись на сервер с паузой и
/// версией, конфликт двух устройств; на главной — «Продолжить создание (шаг N из 7)» игроку и «Создаёт сыщика: шаг …» Хранителю.
/// </summary>
public sealed class CharacterDraftTests : KitContext
{
    private static readonly Guid CampaignId = Guid.Parse("0199b000-0000-7000-8000-000000000701");
    private static readonly Guid PlayerId = Guid.Parse("0199b000-0000-7000-8000-000000000702");
    private static readonly DateTimeOffset Noon = new(2026, 10, 4, 12, 0, 0, TimeSpan.Zero);

    private static InvestigatorDraft Draft(string name, CreationStep step) =>
        new() { StepIndex = (int)step, Personal = new PersonalInfo { Name = name } };

    private static CharacterDraftDto Server(InvestigatorDraft draft, uint version, DateTimeOffset updatedAt) =>
        new(CampaignId, PlayerId, draft, version, updatedAt);

    // ── Какой черновик брать ─────────────────────────────────────────────────

    [Fact]
    public void Local_draft_wins_when_the_server_has_none_or_it_holds_unsent_edits_or_is_newer()
    {
        var local = CmJson.Serialize(Draft("Ада", CreationStep.Skills));
        var server = Server(Draft("Ада", CreationStep.Occupation), 7, Noon);

        Assert.True(WizardDraftStore.PreferLocal(local, Noon, null, null));
        Assert.True(WizardDraftStore.PreferLocal(local, Noon.AddHours(-1), 7, server)); // правки поверх той же версии, не ушли
        Assert.True(WizardDraftStore.PreferLocal(local, Noon.AddMinutes(5), 6, server)); // записан здесь позже
        Assert.False(WizardDraftStore.PreferLocal(local, Noon.AddMinutes(-5), 6, server)); // другое устройство свежее
        Assert.False(WizardDraftStore.PreferLocal(local, null, null, server)); // черновик до сервера, без отметки времени
        Assert.False(WizardDraftStore.PreferLocal(null, null, null, server));
        Assert.False(WizardDraftStore.PreferLocal(local, Noon.AddDays(1), 1, Server(Draft("Ада", CreationStep.Skills), 9, Noon))); // одинаковые
    }

    // ── Запись на сервер ─────────────────────────────────────────────────────

    [Fact]
    public async Task Edits_go_to_local_storage_at_once_and_to_the_server_after_a_pause_with_the_version()
    {
        var api = new FakeDrafts { Stored = Server(Draft("Ада", CreationStep.Method), 3, Time.GetUtcNow()) };
        using var store = new WizardDraftStore(new BrowserStorage(JSInterop.JSRuntime), api, Time, "cm.test", CampaignId);
        var draft = await store.LoadAsync(Xunit.TestContext.Current.CancellationToken);
        Assert.Equal("Ада", draft!.Personal.Name);

        draft.Personal.Name = "Ада Б";
        await store.SaveAsync(draft);
        draft.StepIndex = (int)CreationStep.Characteristics;
        await store.SaveAsync(draft);
        Assert.Empty(api.Saves);
        Assert.Contains(JSInterop.Invocations, i => i.Identifier == "localStorage.setItem" && (string?)i.Arguments[0] == "cm.test");

        Time.Advance(WizardDraftStore.ServerDelay);
        await Until(() => api.Saves.Count == 1);

        var (sent, version) = Assert.Single(api.Saves);
        Assert.Equal(3u, version);
        Assert.Equal("Ада Б", sent.Personal.Name);
        Assert.Equal((int)CreationStep.Characteristics, sent.StepIndex);
    }

    [Fact]
    public async Task Write_from_another_device_stops_saving_until_the_player_keeps_one_version()
    {
        var api = new FakeDrafts { Stored = Server(Draft("Ада", CreationStep.Method), 3, Time.GetUtcNow()) };
        using var store = new WizardDraftStore(new BrowserStorage(JSInterop.JSRuntime), api, Time, "cm.test", CampaignId);
        var draft = (await store.LoadAsync(Xunit.TestContext.Current.CancellationToken))!;
        api.Stored = Server(Draft("С планшета", CreationStep.Biography), 4, Time.GetUtcNow()); // записали на другом устройстве

        draft.Personal.Name = "С телефона";
        await store.SaveAsync(draft);
        Time.Advance(WizardDraftStore.ServerDelay);
        await Until(() => store.Conflict);

        await store.SaveAsync(draft);
        Time.Advance(WizardDraftStore.ServerDelay);
        Assert.Single(api.Saves); // отказанная запись — и больше никаких, пока игрок не выберет

        await store.KeepMineAsync(draft);
        Assert.False(store.Conflict);
        Assert.Equal(("С телефона", (uint?)4u), (api.Saves[^1].Draft.Personal.Name, api.Saves[^1].Version));
    }

    // ── Помощник ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Wizard_in_a_campaign_opens_the_draft_from_the_server_on_its_step()
    {
        var api = new FakeDrafts { Stored = Server(Draft("Ада", CreationStep.Characteristics), 3, Time.GetUtcNow()) };
        SetUpWizard(api);
        Services.GetRequiredService<NavigationManager>().NavigateTo($"character/wizard?campaignId={CampaignId}");

        var cut = Render<InvestigatorWizardPage>();
        await cut.WaitForStateAsync(() => cut.FindAll("[data-testid=wizard-steps]").Count > 0);

        Assert.NotNull(cut.Find("[data-testid=wizard-step-1]").GetAttribute("aria-current"));
        Assert.Contains("Ада", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Wizard_shows_the_conflict_and_takes_the_other_devices_draft_on_request()
    {
        var api = new FakeDrafts { Stored = Server(Draft("Ада", CreationStep.Method), 3, Time.GetUtcNow()) };
        SetUpWizard(api);
        Services.GetRequiredService<NavigationManager>().NavigateTo($"character/wizard?campaignId={CampaignId}");
        var cut = Render<InvestigatorWizardPage>();
        await cut.WaitForStateAsync(() => cut.FindAll("[data-testid=investigator-name]").Count > 0);
        api.Stored = Server(Draft("С планшета", CreationStep.Characteristics), 4, Time.GetUtcNow());

        cut.Find("[data-testid=investigator-name]").Input("С телефона");
        Time.Advance(WizardDraftStore.ServerDelay);
        cut.WaitForAssertion(() => cut.Find("[data-testid=draft-conflict]"));

        cut.Find("[data-testid=draft-take-server]").Click();

        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll("[data-testid=draft-conflict]")));
        Assert.NotNull(cut.Find("[data-testid=wizard-step-1]").GetAttribute("aria-current"));
        Assert.Contains("С планшета", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Restart_removes_the_draft_on_the_server_too()
    {
        var api = new FakeDrafts { Stored = Server(Draft("Ада", CreationStep.Method), 3, Time.GetUtcNow()) };
        SetUpWizard(api);
        Services.GetRequiredService<NavigationManager>().NavigateTo($"character/wizard?campaignId={CampaignId}");
        var host = Render<DialogHost>();
        var cut = Render<InvestigatorWizardPage>();
        await cut.WaitForStateAsync(() => cut.FindAll("[data-testid=wizard-restart]").Count > 0);

        cut.Find("[data-testid=wizard-restart]").Click();
        host.WaitForElement("[data-testid='confirm-dialog-ok']").Click();

        cut.WaitForAssertion(() => Assert.Equal(1, api.Deletes));
        Assert.Null(api.Stored);
    }

    private void SetUpWizard(FakeDrafts drafts)
    {
        Services.AddSingleton<ICharacterDraftsApi>(drafts);
        Services.AddSingleton(Fake.Of<ICharactersApi>(new()
        {
            [nameof(ICharactersApi.GetCreationContextAsync)] = _ => Task.FromResult(new CreationContextDto
            {
                Kind = CharacterKind.Player, CanCreate = true, CampaignName = "Маски",
            }),
        }));
        Services.AddSingleton(Fake.Of<ICatalogApi<SkillDto>>(new()
        {
            [nameof(ICatalogApi<SkillDto>.ListAsync)] = _ => Task.FromResult(new CatalogList<SkillDto>([], false)),
        }));
        Services.AddSingleton(Fake.Of<ICatalogApi<OccupationDto>>(new()
        {
            [nameof(ICatalogApi<OccupationDto>.ListAsync)] = _ => Task.FromResult(new CatalogList<OccupationDto>([], false)),
        }));
    }

    // ── Главная ──────────────────────────────────────────────────────────────

    [Fact]
    public void Player_with_a_draft_continues_creation_on_its_step()
    {
        var campaign = new HomeCampaignDto(CampaignId, "Маски", CampaignKind.Campaign, CampaignStatus.Active, CampaignRole.Player, "Хранитель", 1,
            null, [], [], new CharacterDraftSummaryDto((int)CreationStep.Skills, Time.GetUtcNow().AddMinutes(-5)));

        var cut = Render<HomeCampaignCard>(p => p.Add(c => c.Campaign, campaign));

        Assert.Equal("Продолжить создание (шаг 4 из 7)", cut.Find("[data-testid=continue-draft]").TextContent.Trim());
        Assert.Contains($"campaignId={CampaignId}", cut.Find("[data-testid=continue-draft]").GetAttribute("href"), StringComparison.Ordinal);
        Assert.DoesNotContain("Создать сыщика", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("5 минут назад", cut.Find("[data-testid=my-draft]").TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public void Keeper_sees_which_step_the_player_is_on_instead_of_no_sheet_yet()
    {
        var players = new List<HomePlayerDto>
        {
            new(PlayerId, "Дима", [], new CharacterDraftSummaryDto((int)CreationStep.Skills, Time.GetUtcNow().AddHours(-2))),
            new(Guid.NewGuid(), "Оля", []),
        };
        var campaign = new HomeCampaignDto(CampaignId, "Маски", CampaignKind.Campaign, CampaignStatus.Active, CampaignRole.Keeper, null, 2,
            null, players, []);

        var cut = Render<HomeCampaignCard>(p => p.Add(c => c.Campaign, campaign));

        Assert.Equal("Создаёт сыщика: шаг 4 из 7 — Навыки · 2 часа назад", cut.Find("[data-testid=draft-progress]").TextContent.Trim());
        Assert.Single(cut.FindAll("[data-testid=draft-progress]"));
        Assert.Contains("Листа пока нет", cut.Markup, StringComparison.Ordinal); // у Оли
    }

    [Theory]
    [InlineData(30, "только что")]
    [InlineData(60, "минуту назад")]
    [InlineData(22 * 60, "22 минуты назад")]
    [InlineData(3600, "час назад")]
    [InlineData(5 * 3600, "5 часов назад")]
    [InlineData(21 * 3600, "21 час назад")]
    public void Time_ago_reads_in_russian(int seconds, string expected) =>
        Assert.Equal(expected, LocalTime.Ago(Noon.AddSeconds(-seconds), Noon));

    private static async Task Until(Func<bool> condition)
    {
        for (var i = 0; i < 200 && !condition(); i++)
            await Task.Delay(5, Xunit.TestContext.Current.CancellationToken);
        Assert.True(condition());
    }

    /// <summary>Сервер черновиков в памяти: версия растёт на каждую запись, устаревшая — 409 <c>stale</c>, как у настоящего.</summary>
    private sealed class FakeDrafts : ICharacterDraftsApi
    {
        public CharacterDraftDto? Stored { get; set; }

        public List<(InvestigatorDraft Draft, uint? Version)> Saves { get; } = [];

        public int Deletes { get; private set; }

        public Task<CharacterDraftDto?> GetMineAsync(Guid campaignId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Stored is null ? null : Stored with { Draft = CmJson.DeserializeDraft(CmJson.Serialize(Stored.Draft))! });

        public Task<CharacterDraftDto> GetAsync(Guid campaignId, Guid userId, CancellationToken cancellationToken = default) =>
            Stored is null ? throw new ApiException("Не найдено.", HttpStatusCode.NotFound, null) : Task.FromResult(Stored);

        public Task<CharacterSavedDto> SaveAsync(Guid campaignId, InvestigatorDraft draft, uint? version, CancellationToken cancellationToken = default)
        {
            Saves.Add((draft, version));
            if (Stored?.Version != version)
                throw new ApiException("Запись изменили на другом устройстве.", HttpStatusCode.Conflict, ApiProblemCodes.Stale);

            Stored = new CharacterDraftDto(campaignId, PlayerId, draft, (version ?? 0) + 1, Noon);
            return Task.FromResult(new CharacterSavedDto(Stored.Version, Stored.UpdatedAt));
        }

        public Task DeleteAsync(Guid campaignId, CancellationToken cancellationToken = default)
        {
            Deletes++;
            Stored = null;
            return Task.CompletedTask;
        }
    }
}
