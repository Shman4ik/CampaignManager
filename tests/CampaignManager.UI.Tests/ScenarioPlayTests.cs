using System.Reflection;
using Bunit;
using Bunit.TestDoubles;
using CampaignManager.Contracts.Catalogs;
using CampaignManager.Contracts.Characters;
using CampaignManager.Contracts.Identity;
using CampaignManager.Contracts.Music;
using CampaignManager.Contracts.Scenarios;
using CampaignManager.Core;
using CampaignManager.Core.Campaigns;
using CampaignManager.Core.Scenarios;
using CampaignManager.UI.Scenarios;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CampaignManager.UI.Tests;

/// <summary>
/// Режим игры сценария (T2.5b): всё открытое — в адресе (переходы с replace), проверка локации открывает общий диалог по
/// сыщикам кампании прохождения, показ раздатки не выводит пометку и заметку Хранителя.
/// </summary>
public sealed class ScenarioPlayTests : KitContext
{
    private const string KeeperNote = "выдать после второй ночи";
    private const string HandoutName = "Пометка: письмо тётушки";
    private const string PlayerText = "Дорогой племянник, приезжай скорее";

    private static readonly Guid ScenarioId = Guid.Parse("0199b000-0000-7000-8000-000000000100");
    private static readonly Guid HouseId = Guid.Parse("0199b000-0000-7000-8000-000000000101");
    private static readonly Guid CellarId = Guid.Parse("0199b000-0000-7000-8000-000000000102");
    private static readonly Guid HandoutId = Guid.Parse("0199b000-0000-7000-8000-000000000103");
    private static readonly Guid RunId = Guid.Parse("0199b000-0000-7000-8000-000000000104");

    public ScenarioPlayTests()
    {
        Services.AddSingleton(Fake.Of<IScenariosApi>(new()
        {
            [nameof(IScenariosApi.ListRunsAsync)] = _ => Task.FromResult<IReadOnlyList<ScenarioRunDto>>(
            [
                new(RunId, TableFakes.CampaignId, "Маски", CampaignKind.Campaign, ScenarioRunStatus.Running, null, null, false, [], true, true, ScenarioId, "Дом на холме"),
            ]),
        }));
        Services.AddSingleton<ICharactersApi>(new TableFakes.Characters());
        Services.AddSingleton<ICatalogApi<SkillDto>>(new TableFakes.Skills());
        Services.AddSingleton(Fake.Of<ICatalogApi<MusicTrackDto>>(new()));
        AddAuthorization().SetAuthorized("Хранитель").SetPolicies(Policies.Keeper);
    }

    private static ScenarioDto Scenario() => new()
    {
        Id = ScenarioId,
        Name = "Дом на холме",
        CanEdit = true,
        Locations =
        [
            new()
            {
                Id = HouseId, Name = "Дом",
                Checks = [new() { Id = Guid.NewGuid(), LocationId = HouseId, TargetKind = CheckTarget.Skill, SkillId = TableFakes.SpotHidden.Id, SkillName = "Внимание", Difficulty = Difficulty.Hard, OnSuccess = "Находит дневник." }],
            },
            new() { Id = CellarId, ParentId = HouseId, Name = "Подвал" },
        ],
        Handouts = [new(HandoutId, 0, HandoutName, PlayerText, KeeperNote, null, null)],
    };

    private IRenderedComponent<ScenarioPlay> Play(Guid? location = null, Guid? handout = null) =>
        Render<ScenarioPlay>(p => p
            .Add(c => c.Scenario, Scenario())
            .Add(c => c.LocationId, location)
            .Add(c => c.HandoutId, handout));

    [Fact]
    public void Location_change_goes_to_the_address_with_replace_and_closes_the_showcase()
    {
        var navigation = Services.GetRequiredService<BunitNavigationManager>();
        var cut = Play(HouseId, HandoutId);

        Assert.Contains("Дом", cut.Find("[data-testid=play-location-detail]").TextContent);
        cut.FindAll("[data-testid=play-location]").Single(b => b.TextContent.Contains("Подвал")).Click();

        var last = navigation.History.First();
        Assert.True(last.Options.ReplaceHistoryEntry);
        Assert.EndsWith($"scenarios/{ScenarioId}?mode=play&location={CellarId}", last.Uri);
    }

    [Fact]
    public void Location_check_opens_the_shared_dialog_with_investigators_of_the_run()
    {
        var cut = Play(HouseId);

        cut.WaitForAssertion(() => Assert.Contains("Маски", cut.Find("[data-testid=play-run]").TextContent));
        cut.Find("[data-testid=run-check]").Click();

        var dialog = cut.Find("[data-testid=skill-check-modal]");
        Assert.Contains("Проверка: Внимание", dialog.TextContent);
        Assert.Contains("Харви Уолтерс (Аня)", dialog.TextContent);
    }

    [Fact]
    public void Showcase_shows_what_players_see_and_never_keeper_notes()
    {
        var cut = Play(handout: HandoutId);

        var showcase = cut.Find("[data-testid=handout-showcase]");
        Assert.Contains(PlayerText, showcase.TextContent);
        Assert.DoesNotContain(KeeperNote, showcase.TextContent);
        Assert.DoesNotContain(HandoutName, showcase.TextContent);
        Assert.EndsWith($"scenarios/{ScenarioId}/handouts/{HandoutId}", cut.Find("[data-testid=second-screen]").GetAttribute("href"));

        // Экран Хранителя (панель фактов) пометку показывает — показ игрокам нет.
        Assert.Contains(KeeperNote, cut.Find("[data-testid=play-facts]").TextContent);
    }

    [Fact]
    public void Facts_panel_opens_on_handouts_and_keeps_fact_kinds_collapsed_until_asked()
    {
        var scenario = Scenario();
        scenario.KeyFacts = [new(Guid.NewGuid(), 0, KeyFactType.Backstory, "Что было", "Старый дом.")];
        var cut = Render<PlayFactsPanel>(p => p.Add(c => c.Scenario, scenario));

        // Раздатки первыми: «Показать игрокам» — на первом экране, без прокрутки через факты.
        Assert.Single(cut.FindAll("[data-testid=show-handout]"));
        Assert.Empty(cut.FindAll("[data-testid=play-key-facts]"));

        cut.FindAll("[role=tab]").Single(t => t.TextContent.Contains("Факты")).Click();
        Assert.Empty(cut.FindAll("[data-testid=play-handout]"));
        Assert.DoesNotContain("Старый дом.", cut.Markup);

        cut.Find("[data-testid=toggle-all-facts]").Click();
        Assert.Contains("Старый дом.", cut.Markup);
    }

    [Fact]
    public void Outcomes_of_the_only_check_are_open_at_once()
    {
        var cut = Play(HouseId);

        Assert.Contains("Находит дневник.", cut.Find("[data-testid=play-checks]").TextContent);
        cut.Find("[data-testid=play-check] button[aria-expanded]").Click();
        Assert.DoesNotContain("Находит дневник.", cut.Find("[data-testid=play-checks]").TextContent);
    }

    [Fact]
    public void Checks_come_before_the_location_description()
    {
        var scenario = Scenario();
        scenario.Locations[0].Description = "Длинное описание дома.";
        var cut = Render<ScenarioPlay>(p => p.Add(c => c.Scenario, scenario).Add(c => c.LocationId, HouseId));

        var markup = cut.Find("[data-testid=play-location-detail]").InnerHtml;
        Assert.True(markup.IndexOf("play-checks", StringComparison.Ordinal) < markup.IndexOf("Длинное описание дома.", StringComparison.Ordinal));
    }

    [Fact]
    public void Empty_scenario_shows_one_line_instead_of_three_columns()
    {
        var cut = Render<ScenarioPlay>(p => p.Add(c => c.Scenario, new ScenarioDto { Id = ScenarioId, Name = "Пустой", CanEdit = true }));

        Assert.NotNull(cut.Find("[data-testid=play-empty]"));
        Assert.Empty(cut.FindAll("[data-testid=play-locations]"));
    }

    [Fact]
    public void Overview_keeps_the_scenario_text_collapsed()
    {
        var scenario = Scenario();
        scenario.BodyMd = "Стена текста сценария.";
        var cut = Render<ScenarioPlay>(p => p.Add(c => c.Scenario, scenario));

        Assert.NotNull(cut.Find("[data-testid=play-body]"));
        Assert.Null(cut.Find("[data-testid=play-body]").GetAttribute("open"));
    }

    [Fact]
    public void Play_link_keeps_location_handout_and_run_in_the_address() =>
        Assert.Equal($"scenarios/{ScenarioId}?mode=play&location={HouseId}&handout={HandoutId}&run={RunId}",
            ScenarioLinks.Play(ScenarioId, HouseId, HandoutId, RunId));
}

/// <summary>Подделка интерфейса API: отвечает только на перечисленные методы, остальное бросает.</summary>
public class Fake : DispatchProxy
{
    private Dictionary<string, Func<object?[]?, object?>> _handlers = [];

    public static T Of<T>(Dictionary<string, Func<object?[]?, object?>> handlers)
        where T : class
    {
        var proxy = Create<T, Fake>();
        ((Fake)(object)proxy)._handlers = handlers;
        return proxy;
    }

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) =>
        targetMethod is not null && _handlers.TryGetValue(targetMethod.Name, out var handler)
            ? handler(args)
            : throw new NotSupportedException(targetMethod?.Name);
}
