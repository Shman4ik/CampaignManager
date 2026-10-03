using Bunit;
using CampaignManager.Contracts.Scenarios;
using CampaignManager.Core;
using CampaignManager.Core.Campaigns;
using CampaignManager.Core.Characters;
using CampaignManager.UI.Encounters;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CampaignManager.UI.Tests;

/// <summary>
/// Окно участника, вкладка «Сценарий» (T2.6d): сцена в кампании предлагает сценарии её незавершённых прохождений и отдаёт
/// выбранное прохождение странице; без кампании или без прохождений — любой сценарий библиотеки, прохождение не пишется.
/// </summary>
public sealed class ParticipantPickerTests : KitContext
{
    private static readonly Guid CampaignId = Guid.Parse("0199b000-0000-7000-8000-000000000200");
    private static readonly Guid FogScenario = Guid.Parse("0199b000-0000-7000-8000-000000000201");
    private static readonly Guid MasksScenario = Guid.Parse("0199b000-0000-7000-8000-000000000202");
    private static readonly Guid FogRun = Guid.Parse("0199b000-0000-7000-8000-000000000203");
    private static readonly Guid MasksRun = Guid.Parse("0199b000-0000-7000-8000-000000000204");

    private readonly List<Guid?> _loadedScenarios = [];
    private IReadOnlyList<ScenarioRunDto> _runs = [];

    public ParticipantPickerTests()
    {
        Services.AddSingleton(Fake.Of<IScenariosApi>(new()
        {
            [nameof(IScenariosApi.ListAsync)] = _ => Task.FromResult(new ScenarioListDto(
            [
                new ScenarioSummaryDto { Id = FogScenario, Name = "Туман" },
                new ScenarioSummaryDto { Id = MasksScenario, Name = "Маски" },
                new ScenarioSummaryDto { Id = Guid.NewGuid(), Name = "Чужой сценарий" },
            ], true)),
        }));
        Services.AddSingleton(Fake.Of<IRunsApi>(new()
        {
            [nameof(IRunsApi.ListForCampaignAsync)] = _ => Task.FromResult(_runs),
        }));
    }

    private static ScenarioRunDto Run(Guid id, Guid scenarioId, string scenario) =>
        new(id, CampaignId, "Кампания", CampaignKind.Campaign, ScenarioRunStatus.Running, null, null, false, [], true, true, scenarioId, scenario);

    private IRenderedComponent<ParticipantPicker> Open(Guid? campaignId, Guid? runId, Action<Guid>? onRunChosen = null) =>
        Render<ParticipantPicker>(p => p
            .Add(c => c.Open, true)
            .Add(c => c.Sources, [new RecordingScenarioSource(_loadedScenarios)])
            .Add(c => c.Context, new ParticipantPickerContext(campaignId, new SkillCatalog([])))
            .Add(c => c.RunId, runId)
            .Add(c => c.OnRunChosen, id => onRunChosen?.Invoke(id)));

    [Fact]
    public void Campaign_scene_offers_scenarios_of_unfinished_runs_and_reports_the_chosen_run()
    {
        _runs = [Run(FogRun, FogScenario, "Туман"), Run(MasksRun, MasksScenario, "Маски")];
        Guid? chosen = null;

        var cut = Open(CampaignId, null, id => chosen = id);

        Assert.Empty(cut.FindAll("[data-testid=picker-scenario]"));
        var options = cut.Find("[data-testid=picker-run]").QuerySelectorAll("option").Select(o => o.TextContent.Trim()).ToList();
        Assert.Equal(3, options.Count);
        Assert.Contains(options, o => o.StartsWith("Туман", StringComparison.Ordinal));
        Assert.DoesNotContain(options, o => o.Contains("Чужой", StringComparison.Ordinal));

        cut.Find("[data-testid=picker-run]").Change(MasksRun.ToString());

        Assert.Equal(MasksRun, chosen);
        Assert.Equal(MasksScenario, _loadedScenarios[^1]);
    }

    [Fact]
    public void Scene_with_a_run_opens_on_its_scenario_and_does_not_rewrite_the_same_run()
    {
        _runs = [Run(FogRun, FogScenario, "Туман"), Run(MasksRun, MasksScenario, "Маски")];
        Guid? chosen = null;

        var cut = Open(CampaignId, MasksRun, id => chosen = id);

        Assert.Equal(MasksRun.ToString(), cut.Find("[data-testid=picker-run]").GetAttribute("value"));
        Assert.Equal(MasksScenario, _loadedScenarios[^1]);

        cut.Find("[data-testid=picker-run]").Change(MasksRun.ToString());
        Assert.Null(chosen);
    }

    [Fact]
    public void Scene_without_campaign_or_runs_offers_any_scenario_and_writes_no_run()
    {
        Guid? chosen = null;

        var outside = Open(null, null, id => chosen = id);
        Assert.Empty(outside.FindAll("[data-testid=picker-run]"));
        Assert.Equal(4, outside.Find("[data-testid=picker-scenario]").QuerySelectorAll("option").Length);
        Assert.Empty(outside.FindAll("[data-testid=picker-no-runs]"));

        _runs = [];
        var empty = Open(CampaignId, null, id => chosen = id);
        Assert.Equal(4, empty.Find("[data-testid=picker-scenario]").QuerySelectorAll("option").Length);
        Assert.Single(empty.FindAll("[data-testid=picker-no-runs]"));

        empty.Find("[data-testid=picker-scenario]").Change(FogScenario.ToString());
        Assert.Null(chosen);
        Assert.Equal(FogScenario, _loadedScenarios[^1]);
    }

    private static readonly Guid FogRunOld = Guid.Parse("0199b000-0000-7000-8000-000000000205");

    private IRenderedComponent<ParticipantPicker> OpenWithOtherTab(Guid? runId, bool open = true) =>
        Render<ParticipantPicker>(p => p
            .Add(c => c.Open, open)
            .Add(c => c.Sources, [new OtherSource(), new RecordingScenarioSource(_loadedScenarios)])
            .Add(c => c.Context, new ParticipantPickerContext(CampaignId, new SkillCatalog([])))
            .Add(c => c.RunId, runId));

    [Fact]
    public void Two_runs_of_one_scenario_list_it_once_and_prefer_the_run_of_the_scene()
    {
        // Список API — от свежих к старым: старое прохождение «Тумана» идёт после «Масок».
        _runs = [Run(FogRun, FogScenario, "Туман"), Run(MasksRun, MasksScenario, "Маски"), Run(FogRunOld, FogScenario, "Туман")];

        var fresh = Open(CampaignId, null);
        var options = fresh.Find("[data-testid=picker-run]").QuerySelectorAll("option").Skip(1).ToList();
        Assert.Equal(2, options.Count);
        Assert.Equal([FogRun.ToString(), MasksRun.ToString()], options.Select(o => o.GetAttribute("value")));

        var bound = Open(CampaignId, FogRunOld);
        Assert.Equal(FogRunOld.ToString(), bound.Find("[data-testid=picker-run]").GetAttribute("value"));
        Assert.Equal(2, bound.Find("[data-testid=picker-run]").QuerySelectorAll("option").Skip(1).Count());
        Assert.Equal(FogScenario, _loadedScenarios[^1]);
    }

    [Fact]
    public void Scene_run_is_preselected_when_the_scenario_tab_is_opened_later_and_the_choice_survives_tabs_and_reopening()
    {
        _runs = [Run(FogRun, FogScenario, "Туман"), Run(MasksRun, MasksScenario, "Маски")];
        var chosen = new List<Guid>();
        var cut = Render<ParticipantPicker>(p => p
            .Add(c => c.Open, true)
            .Add(c => c.Sources, [new OtherSource(), new RecordingScenarioSource(_loadedScenarios)])
            .Add(c => c.Context, new ParticipantPickerContext(CampaignId, new SkillCatalog([])))
            .Add(c => c.RunId, FogRun)
            .Add(c => c.OnRunChosen, id => chosen.Add(id)));

        cut.FindAll("[role=tab]").Single(t => t.TextContent.Contains("Сценарий", StringComparison.Ordinal)).Click();
        Assert.Equal(FogRun.ToString(), cut.Find("[data-testid=picker-run]").GetAttribute("value"));
        Assert.Equal(FogScenario, _loadedScenarios[^1]);

        cut.Find("[data-testid=picker-run]").Change(MasksRun.ToString());
        cut.FindAll("[role=tab]").First().Click();
        cut.FindAll("[role=tab]").Single(t => t.TextContent.Contains("Сценарий", StringComparison.Ordinal)).Click();
        Assert.Equal(MasksRun.ToString(), cut.Find("[data-testid=picker-run]").GetAttribute("value"));

        cut.Render(p => p.Add(c => c.Open, false));
        cut.Render(p => p.Add(c => c.Open, true).Add(c => c.RunId, MasksRun));
        Assert.Equal(MasksRun.ToString(), cut.Find("[data-testid=picker-run]").GetAttribute("value"));
        Assert.Equal([MasksRun], chosen);
    }

    private sealed class OtherSource : IParticipantSource
    {
        public string Key => "other";

        public string Label => "Сыщики";

        public string Icon => "fa-user";

        public Task<ParticipantSourceResult> LoadAsync(ParticipantPickerContext context, CancellationToken cancellationToken) =>
            Task.FromResult(new ParticipantSourceResult([]));
    }

    private sealed class RecordingScenarioSource(List<Guid?> loaded) : IParticipantSource
    {
        public string Key => "scenario";

        public string Label => "Сценарий";

        public string Icon => "fa-masks-theater";

        public bool UsesScenario => true;

        public Task<ParticipantSourceResult> LoadAsync(ParticipantPickerContext context, CancellationToken cancellationToken)
        {
            loaded.Add(context.ScenarioId);
            return Task.FromResult(new ParticipantSourceResult([]));
        }
    }
}
