using Bunit;
using CampaignManager.Contracts.Identity;
using CampaignManager.Core.Encounters;
using CampaignManager.UI.KeeperScreen;
using CampaignManager.UI.Shared;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CampaignManager.UI.Tests;

/// <summary>
/// Ширма Хранителя: кнопка в шапке только Хранителю, панель поверх экрана, разделы по адресу. Содержимое
/// таблиц — из Core (<c>Core.Tests/KeeperScreen</c>, <c>Encounters/AttackModifiersTests</c>).
/// </summary>
public sealed class KeeperScreenTests : KitContext
{
    public KeeperScreenTests()
    {
        Services.AddSingleton<CampaignManager.Contracts.Campaigns.ICampaignsApi>(new TableFakes.Campaigns());
        Services.AddSingleton<CampaignManager.Contracts.Characters.ICharactersApi>(new TableFakes.Characters());
        Services.AddSingleton<CampaignManager.Contracts.Catalogs.ICatalogApi<CampaignManager.Contracts.Catalogs.SkillDto>>(new TableFakes.Skills());
    }

    // Групповая проверка с составом кампании (T2.3): значения навыка — с листов сыщиков, а не вписанные руками.
    [Fact]
    public void Group_check_takes_party_from_keepers_campaign()
    {
        AddAuthorization().SetAuthorized("Хранитель").SetPolicies(Policies.Keeper);
        Services.GetRequiredService<NavigationManager>().NavigateTo("reference?block=group");
        var cut = Render<KeeperReferencePage>();

        cut.Find("[data-testid='group-campaign']").Change(TableFakes.CampaignId.ToString());

        cut.WaitForAssertion(() =>
        {
            var text = cut.Find("[data-testid='keeper-screen-block-group']").TextContent;
            Assert.Contains("Харви Уолтерс", text);
            Assert.Contains("65", text);
            Assert.Contains("Нора Флинн", text);
        });
    }

    private IRenderedComponent<IComponent> PageWithHost() => Render(builder =>
    {
        builder.OpenComponent<PageHeader>(0);
        builder.AddAttribute(1, nameof(PageHeader.Title), "Бой");
        builder.CloseComponent();
        builder.OpenComponent<KeeperScreenHost>(2);
        builder.CloseComponent();
    });

    [Fact]
    public void Player_sees_no_keeper_screen_button()
    {
        AddAuthorization().SetAuthorized("Игрок");

        var cut = PageWithHost();

        Assert.Empty(cut.FindAll("[data-testid='keeper-screen-toggle']"));
    }

    [Fact]
    public void Keeper_opens_drawer_from_page_header()
    {
        AddAuthorization().SetAuthorized("Хранитель").SetPolicies(Policies.Keeper);
        var cut = PageWithHost();

        cut.Find(".cm-topbar [data-testid='keeper-screen-toggle']").Click();

        var drawer = cut.Find("dialog.cm-modal-drawer");
        Assert.Contains("Ширма Хранителя", drawer.TextContent);
        Assert.NotNull(cut.Find("[data-testid='keeper-screen-block-checks']"));
        Assert.Equal("reference?block=checks", cut.Find("[data-testid='keeper-screen-page']").GetAttribute("href"));
    }

    [Fact]
    public void Tabs_switch_block_and_page_link_follows()
    {
        AddAuthorization().SetAuthorized("Хранитель").SetPolicies(Policies.Keeper);
        Services.GetRequiredService<KeeperScreenState>().Show(KeeperScreenBlock.Checks);
        var cut = PageWithHost();

        cut.FindAll("[role='tab']").Single(t => t.TextContent.Contains("Стрельба")).Click();

        Assert.NotNull(cut.Find("[data-testid='keeper-screen-block-firearms']"));
        Assert.Equal(AttackModifiers.Table.Count(r => r.Kind == AttackKind.Ranged), cut.FindAll("[data-testid='modifier-row']").Count);
        Assert.Equal("reference?block=firearms", cut.Find("[data-testid='keeper-screen-page']").GetAttribute("href"));
    }

    [Theory]
    [InlineData("luck", "keeper-screen-block-luck")]
    [InlineData("GROUP", "keeper-screen-block-group")]
    [InlineData("нет-такого", "keeper-screen-block-checks")]
    [InlineData(null, "keeper-screen-block-checks")]
    public void Reference_page_opens_block_from_address(string? block, string testId)
    {
        AddAuthorization().SetAuthorized("Хранитель").SetPolicies(Policies.Keeper);
        Services.GetRequiredService<NavigationManager>().NavigateTo(block is null ? "reference" : $"reference?block={block}");

        var cut = Render<KeeperReferencePage>();

        Assert.NotNull(cut.Find($"[data-testid='{testId}']"));
        Assert.Empty(cut.FindAll("[data-testid='keeper-screen-toggle']"));
    }

    [Fact]
    public void Reference_page_tab_changes_address()
    {
        AddAuthorization().SetAuthorized("Хранитель").SetPolicies(Policies.Keeper);
        var navigation = Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo("reference?block=checks");
        var cut = Render<KeeperReferencePage>();

        cut.FindAll("[role='tab']").Single(t => t.TextContent.Contains("Рассудок")).Click();

        Assert.EndsWith("reference?block=sanity", navigation.Uri);
    }

    [Fact]
    public void Checks_block_recalculates_targets_from_core()
    {
        var cut = Render<KeeperReference>(p => p.Add(c => c.ActiveBlock, KeeperScreenBlock.Checks));

        cut.Find("[data-testid='ks-skill']").Input("55");

        var rows = cut.FindAll("[data-testid='ks-targets'] tbody tr");
        Assert.Contains("01–55", rows[0].TextContent);
        Assert.Contains("01–27", rows[1].TextContent);
        Assert.Contains("96–100", rows[1].TextContent); // трудная проверка навыка 55: нужно 27 — крах с 96 (стр. 88)
        Assert.Contains("01–11", rows[2].TextContent);
    }

    [Fact]
    public void Luck_block_never_sells_a_critical()
    {
        var cut = Render<KeeperReference>(p => p.Add(c => c.ActiveBlock, KeeperScreenBlock.Luck));

        cut.Find("[data-testid='ks-luck-roll']").Input("40");
        cut.Find("[data-testid='ks-luck-skill']").Input("7");

        var text = cut.Find("[data-testid='ks-luck-options']").TextContent;
        Assert.DoesNotContain("чрезвычайный", text); // порог чрезвычайного при навыке 7 — 01 (F-S03)
        Assert.Contains("обычный успех — 33 (станет 7)", text);
    }

    [Fact]
    public void Sanity_block_threshold_from_rules()
    {
        var cut = Render<KeeperReference>(p => p.Add(c => c.ActiveBlock, KeeperScreenBlock.Sanity));

        cut.Find("[data-testid='ks-sanity']").Input("52");

        Assert.Contains("11", cut.Find("[data-testid='ks-sanity-threshold']").TextContent); // ⅕ вверх: 52 → 11
    }
}
