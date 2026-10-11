using System.Net;
using Bunit;
using Bunit.TestDoubles;
using CampaignManager.Contracts.Identity;
using CampaignManager.Contracts.Scenarios;
using CampaignManager.UI.Scenarios;
using CampaignManager.UI.Shared;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CampaignManager.UI.Tests;

/// <summary>
/// Избранные сценарии: личная звёздочка на карточке библиотеки и в шапке рабочего места, фильтр «Избранное» и порядок —
/// отмеченные первыми.
/// </summary>
public sealed class ScenarioFavoritesTests : KitContext
{
    private readonly List<(Guid Id, bool Favorite)> _calls = [];
    private HttpStatusCode? _failWith;

    private static ScenarioSummaryDto Summary(string name, bool favorite = false) => new()
    {
        Id = Guid.NewGuid(),
        Name = name,
        IsFavorite = favorite,
        IsAuthor = true,
        UpdatedAt = DateTimeOffset.UnixEpoch,
        CanEdit = true,
        CanDelete = true,
    };

    private const string FilterKey = "cm.scenarios-favorites-only";

    private IRenderedComponent<ScenariosPage> Library(params ScenarioSummaryDto[] items) => Library(storedFilter: false, items);

    private IRenderedComponent<ScenariosPage> Library(bool storedFilter, params ScenarioSummaryDto[] items)
    {
        if (storedFilter)
            JSInterop.Setup<string?>("localStorage.getItem", FilterKey).SetResult("1");

        AddAuthorization().SetAuthorized("Хранитель").SetPolicies(Policies.Keeper);
        Services.AddSingleton(Fake.Of<IScenariosApi>(new()
        {
            [nameof(IScenariosApi.ListAsync)] = _ => Task.FromResult(new ScenarioListDto(items, true)),
            [nameof(IScenariosApi.SetFavoriteAsync)] = args =>
            {
                if (_failWith is { } status)
                    throw new HttpRequestException("отказ", null, status);

                _calls.Add(((Guid)args![0]!, (bool)args[1]!));
                return Task.CompletedTask;
            },
        }));
        Services.AddSingleton(Fake.Of<IScenarioExchangeApi>(new()));
        var cut = Render<ScenariosPage>();
        // Со сохранённым фильтром карточек может быть меньше, чем сценариев: тест ждёт сам.
        if (!storedFilter)
            cut.WaitForState(() => cut.FindAll("[data-testid=scenario-card]").Count == items.Length);

        return cut;
    }

    private static string[] Titles(IRenderedComponent<ScenariosPage> cut) =>
        [.. cut.FindAll("[data-testid=scenario-card] a.cm-card-title").Select(a => a.TextContent.Trim())];

    [Fact]
    public void Library_puts_favorites_first_and_labels_the_star_by_state()
    {
        var cut = Library(Summary("Башня"), Summary("Яма", favorite: true));

        Assert.Equal(["Яма", "Башня"], Titles(cut));
        Assert.NotNull(cut.Find("[aria-label='Убрать из избранного: Яма']"));
        Assert.NotNull(cut.Find("[aria-label='Добавить в избранное: Башня']"));
    }

    [Fact]
    public void Tapping_the_star_writes_it_and_lifts_the_card()
    {
        var tower = Summary("Башня");
        var cut = Library(Summary("Аркхем"), tower);

        cut.Find("[aria-label='Добавить в избранное: Башня']").Click();

        cut.WaitForAssertion(() => Assert.Equal(["Башня", "Аркхем"], Titles(cut)));
        Assert.Equal([(tower.Id, true)], _calls);
        Assert.NotNull(cut.Find("[aria-label='Убрать из избранного: Башня']"));
    }

    [Fact]
    public void Removing_the_star_writes_it_and_returns_the_card_to_its_place()
    {
        var pit = Summary("Яма", favorite: true);
        var cut = Library(Summary("Аркхем"), pit);

        cut.Find("[aria-label='Убрать из избранного: Яма']").Click();

        cut.WaitForAssertion(() => Assert.Equal(["Аркхем", "Яма"], Titles(cut)));
        Assert.Equal([(pit.Id, false)], _calls);
    }

    [Fact]
    public void A_refused_write_keeps_the_star_as_it_was()
    {
        _failWith = HttpStatusCode.InternalServerError;
        var cut = Library(Summary("Башня"));

        cut.Find("[aria-label='Добавить в избранное: Башня']").Click();

        cut.WaitForAssertion(() => Assert.Contains("Сервер не смог выполнить запрос", cut.Services.GetRequiredService<ToastService>().Messages.Single().Message));
        Assert.NotNull(cut.Find("[aria-label='Добавить в избранное: Башня']"));
        Assert.Empty(_calls);
    }

    [Fact]
    public void The_filter_chip_is_absent_while_nothing_is_starred()
    {
        var cut = Library(Summary("Башня"), Summary("Яма"));

        Assert.Empty(cut.FindAll("[data-testid=scenario-favorites-filter]"));
    }

    [Fact]
    public void The_filter_chip_leaves_only_favorites()
    {
        var cut = Library(Summary("Аркхем"), Summary("Башня"), Summary("Яма", favorite: true));
        cut.Find("[data-testid=scenario-favorites-filter]").Click();

        Assert.Equal(["Яма"], Titles(cut));
        Assert.Equal("true", cut.Find("[data-testid=scenario-favorites-filter]").GetAttribute("aria-pressed"));

        cut.Find("[data-testid=scenario-favorites-filter]").Click();
        Assert.Equal(["Яма", "Аркхем", "Башня"], Titles(cut));
    }

    [Fact]
    public void Unstarring_the_last_favorite_under_the_filter_leaves_an_empty_state_with_a_reset()
    {
        var cut = Library(Summary("Аркхем"), Summary("Яма", favorite: true));
        cut.Find("[data-testid=scenario-favorites-filter]").Click();

        cut.Find("[aria-label='Убрать из избранного: Яма']").Click();

        cut.WaitForAssertion(() => Assert.Contains("Нет сценариев по этим условиям.", cut.Markup, StringComparison.Ordinal));
        cut.Find("button:contains('Сбросить фильтры')").Click();
        Assert.Equal(["Аркхем", "Яма"], Titles(cut));
    }

    [Fact]
    public void Search_and_the_chip_share_one_panel_when_the_library_is_long()
    {
        var items = Enumerable.Range(1, 7).Select(i => Summary($"Сценарий {i}", favorite: i == 7)).ToArray();
        var cut = Library(items);

        Assert.NotNull(cut.Find("[data-testid=scenario-search]"));
        Assert.NotNull(cut.Find("[data-testid=scenario-favorites-filter]"));

        cut.Find("[data-testid=scenario-favorites-filter]").Click();
        Assert.Equal(["Сценарий 7"], Titles(cut));
    }

    [Fact]
    public void The_filter_is_written_to_the_device_and_removed_when_switched_off()
    {
        var cut = Library(Summary("Аркхем"), Summary("Яма", favorite: true));

        cut.Find("[data-testid=scenario-favorites-filter]").Click();
        Assert.Contains(JSInterop.Invocations, i => i.Identifier == "localStorage.setItem" && (string?)i.Arguments[0] == FilterKey && (string?)i.Arguments[1] == "1");

        cut.Find("[data-testid=scenario-favorites-filter]").Click();
        Assert.Contains(JSInterop.Invocations, i => i.Identifier == "localStorage.removeItem" && (string?)i.Arguments[0] == FilterKey);
    }

    [Fact]
    public void A_stored_filter_is_restored_on_entry()
    {
        var cut = Library(storedFilter: true, Summary("Аркхем"), Summary("Башня"), Summary("Яма", favorite: true));

        cut.WaitForAssertion(() => Assert.Equal(["Яма"], Titles(cut)));
        Assert.Equal("true", cut.Find("[data-testid=scenario-favorites-filter]").GetAttribute("aria-pressed"));
    }

    [Fact]
    public void Nothing_is_filtered_without_a_stored_filter()
    {
        var cut = Library(Summary("Аркхем"), Summary("Яма", favorite: true));

        Assert.Equal(["Яма", "Аркхем"], Titles(cut));
        Assert.Equal("false", cut.Find("[data-testid=scenario-favorites-filter]").GetAttribute("aria-pressed"));
    }

    [Fact]
    public void A_stored_filter_is_dropped_when_nothing_is_starred_any_more()
    {
        var cut = Library(storedFilter: true, Summary("Аркхем"), Summary("Башня"));

        cut.WaitForAssertion(() => Assert.Equal(["Аркхем", "Башня"], Titles(cut)));
        Assert.Empty(cut.FindAll("[data-testid=scenario-favorites-filter]"));
        Assert.DoesNotContain("Нет сценариев по этим условиям", cut.Markup, StringComparison.Ordinal);
        Assert.Contains(JSInterop.Invocations, i => i.Identifier == "localStorage.removeItem" && (string?)i.Arguments[0] == FilterKey);
    }
}
