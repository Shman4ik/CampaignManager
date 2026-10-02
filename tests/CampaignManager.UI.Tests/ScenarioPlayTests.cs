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
                new(RunId, TableFakes.CampaignId, "Маски", CampaignKind.Campaign, ScenarioRunStatus.Running, null, null, false, [], true, true),
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
