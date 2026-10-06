using Bunit;
using Bunit.TestDoubles;
using CampaignManager.Contracts.Campaigns;
using CampaignManager.Contracts.Catalogs;
using CampaignManager.Contracts.Characters;
using CampaignManager.Contracts.Identity;
using CampaignManager.Contracts.Music;
using CampaignManager.Contracts.Scenarios;
using CampaignManager.Core;
using CampaignManager.Core.Campaigns;
using CampaignManager.Core.Scenarios;
using CampaignManager.UI.Scenarios;
using CampaignManager.UI.Shared;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CampaignManager.UI.Tests;

/// <summary>
/// Ревью интерфейса g4 (F4a): библиотека и рабочее место сценария — эпоха и автор показываются, только когда несут
/// отличие; завершённая партия свёрнута; «разовая игра» только при готовых сыщиках; факты режима игры — список названий;
/// над локацией одна строка партии.
/// </summary>
public sealed class ScenarioReviewG4Tests : KitContext
{
    private static readonly Guid ScenarioId = Guid.Parse("0199b000-0000-7000-8000-000000000200");
    private static readonly Guid RunId = Guid.Parse("0199b000-0000-7000-8000-000000000201");

    private static ScenarioSummaryDto Summary(string name, bool isAuthor, string? author = "Анна", Era? era = Era.Classic, int runs = 0) => new()
    {
        Id = Guid.NewGuid(),
        Name = name,
        Era = era,
        AuthorName = author,
        IsAuthor = isAuthor,
        RunCount = runs,
        UpdatedAt = DateTimeOffset.UnixEpoch,
        CanEdit = true,
        CanDelete = true,
    };

    private IRenderedComponent<ScenariosPage> Library(params ScenarioSummaryDto[] items)
    {
        AddAuthorization().SetAuthorized("Хранитель").SetPolicies(Policies.Keeper);
        Services.AddSingleton(Fake.Of<IScenariosApi>(new()
        {
            [nameof(IScenariosApi.ListAsync)] = _ => Task.FromResult(new ScenarioListDto(items, true)),
        }));
        Services.AddSingleton(Fake.Of<IScenarioExchangeApi>(new()));
        var cut = Render<ScenariosPage>();
        cut.WaitForState(() => cut.FindAll("[data-testid=scenario-card]").Count == items.Length);
        return cut;
    }

    [Fact]
    public void Library_hides_the_era_while_every_scenario_is_from_one_era_and_the_author_when_it_is_you()
    {
        var cut = Library(Summary("Дом", isAuthor: true, "Дмитрий"), Summary("Лес", isAuthor: false, "Анна"));

        Assert.DoesNotContain("1920-е", cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("Дмитрий", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("Анна · Изменён", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void Library_shows_the_era_once_scenarios_differ_by_it()
    {
        var cut = Library(Summary("Дом", isAuthor: true, era: Era.Classic), Summary("Лес", isAuthor: true, era: Era.Modern));

        Assert.Contains("1920-е", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("Наши дни", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void Remake_with_the_same_title_gets_a_tail_in_the_card_name()
    {
        var original = Summary("Эликсир жизни", isAuthor: true);
        var remake = Summary("Эликсир жизни", isAuthor: true);
        remake.SourceScenarioName = "Эликсир жизни";

        var cut = Library(original, remake);

        Assert.Single(cut.FindAll("a.cm-card-title"), a => a.TextContent.Contains("переделка"));
    }

    [Fact]
    public void Delete_of_a_scenario_with_runs_is_a_disabled_menu_item_with_the_reason()
    {
        var cut = Library(Summary("Дом", isAuthor: true, runs: 2));

        cut.Find("[aria-label='Действия: Дом']").Click();

        var item = cut.Find("[role=menuitem]");
        Assert.True(item.HasAttribute("disabled"));
        Assert.Contains("идёт в кампаниях (2)", item.TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public void Scenario_form_asks_for_the_era_only_when_the_library_has_more_than_one()
    {
        Services.AddSingleton(Fake.Of<IScenariosApi>(new()));

        var without = Render<ScenarioFormModal>(p => p.Add(m => m.Open, true));
        Assert.DoesNotContain("Эпоха", without.Markup, StringComparison.Ordinal);

        var with = Render<ScenarioFormModal>(p => p.Add(m => m.Open, true).Add(m => m.AskEra, true));
        Assert.Contains("Эпоха", with.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void Scenario_form_blocks_an_empty_title_and_asks_for_focus_on_it()
    {
        Services.AddSingleton(Fake.Of<IScenariosApi>(new()));
        var cut = Render<ScenarioFormModal>(p => p.Add(m => m.Open, true));

        cut.Find("#scenario-form").Submit();

        Assert.Equal("true", cut.Find("[data-testid=scenario-name]").GetAttribute("aria-invalid"));
        Assert.Contains("Название обязательно.", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void Description_tab_hides_your_own_authorship_and_the_classic_era_and_edits_through_the_page()
    {
        var edited = false;
        var scenario = new ScenarioDto
        {
            Id = ScenarioId, Name = "Дом", Summary = "Старый особняк.", Setting = "Бостон", SettingDate = "Июнь 1925 года",
            Era = Era.Classic, AuthorName = "Дмитрий", IsAuthor = true, CanEdit = true, UpdatedAt = DateTimeOffset.UnixEpoch,
        };
        Services.AddSingleton(Fake.Of<IScenariosApi>(new()
        {
            [nameof(IScenariosApi.ListRunsAsync)] = _ => Task.FromResult<IReadOnlyList<ScenarioRunDto>>([]),
        }));
        Services.AddSingleton(Fake.Of<IRunsApi>(new()));
        Services.AddSingleton(Fake.Of<ICampaignsApi>(new()));

        var cut = Render<ScenarioDescriptionTab>(p => p
            .Add(c => c.Scenario, scenario)
            .Add(c => c.OnEdit, () => edited = true));

        var facts = cut.Find("[data-testid=scenario-facts]").TextContent;
        Assert.Contains("Июнь 1925 года", facts, StringComparison.Ordinal);
        Assert.DoesNotContain("Автор", facts, StringComparison.Ordinal);
        Assert.DoesNotContain("1920-е", facts, StringComparison.Ordinal);

        cut.Find("[data-testid=edit-scenario]").Click();
        Assert.True(edited);

        scenario.IsAuthor = false;
        cut.Render();
        Assert.Contains("Дмитрий", cut.Find("[data-testid=scenario-facts]").TextContent, StringComparison.Ordinal);
    }

    private static ScenarioRunDto Run(ScenarioRunStatus status, string? announcement = null, string campaign = "Маски") =>
        new(RunId, TableFakes.CampaignId, campaign, CampaignKind.OneShot, status, null, announcement, false,
            [new(Guid.NewGuid(), "Харви Уолтерс", "Аня", null, false)], true, true, ScenarioId, "Дом");

    private IRenderedComponent<ScenarioRunsSection> Runs(ScenarioDto scenario, params ScenarioRunDto[] runs)
    {
        Services.AddSingleton(Fake.Of<IScenariosApi>(new()
        {
            [nameof(IScenariosApi.ListRunsAsync)] = _ => Task.FromResult<IReadOnlyList<ScenarioRunDto>>(runs),
        }));
        Services.AddSingleton(Fake.Of<IRunsApi>(new()));
        Services.AddSingleton(Fake.Of<ICampaignsApi>(new()));
        var cut = Render<ScenarioRunsSection>(p => p.Add(c => c.Scenario, scenario));
        cut.WaitForState(() => cut.FindAll("[data-testid=run-row]").Count == runs.Length);
        return cut;
    }

    [Fact]
    public void Finished_run_is_one_line_and_opens_announcement_and_signups_by_touch()
    {
        var cut = Runs(new ScenarioDto { Id = ScenarioId, Name = "Дом" }, Run(ScenarioRunStatus.Finished, "Длинный анонс завершённой игры."));

        Assert.DoesNotContain("Длинный анонс", cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("Записи ·", cut.Markup, StringComparison.Ordinal);

        cut.Find("[data-testid=run-toggle]").Click();

        Assert.Contains("Длинный анонс", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("Записи · 1", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void Running_run_keeps_announcement_visible_and_play_is_not_primary()
    {
        var cut = Runs(new ScenarioDto { Id = ScenarioId, Name = "Дом" }, Run(ScenarioRunStatus.Running, "Анонс идущей игры."));

        Assert.Contains("Анонс идущей игры.", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("cm-btn-secondary", cut.Find("[data-testid=play-run]").ClassName, StringComparison.Ordinal);
    }

    [Fact]
    public void One_shot_is_not_offered_when_the_scenario_has_no_ready_investigators()
    {
        var cut = Runs(new ScenarioDto { Id = ScenarioId, Name = "Дом" });

        Assert.Empty(cut.FindAll("[data-testid=announce-oneshot]"));
        Assert.NotEmpty(cut.FindAll("[data-testid=play-in-campaign]"));
    }

    [Fact]
    public void One_shot_is_called_a_one_off_game_and_offered_with_ready_investigators()
    {
        var scenario = new ScenarioDto { Id = ScenarioId, Name = "Дом", Pregens = [new(new CharacterSummaryDto { Id = Guid.NewGuid() }, false)] };

        var cut = Runs(scenario);

        Assert.Contains("Объявить разовую игру", cut.Find("[data-testid=announce-oneshot]").TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public void Facts_in_play_mode_are_a_list_of_titles_opened_one_by_one()
    {
        var scenario = new ScenarioDto
        {
            Id = ScenarioId, Name = "Дом",
            KeyFacts =
            [
                new(Guid.NewGuid(), 0, KeyFactType.Backstory, "Что было", "Старый дом."),
                new(Guid.NewGuid(), 1, KeyFactType.Truth, "Правда", "Жил призрак."),
            ],
        };
        var cut = Render<PlayFactsPanel>(p => p.Add(c => c.Scenario, scenario));

        Assert.Equal(2, cut.FindAll("[data-testid=play-fact]").Count);
        Assert.DoesNotContain("Старый дом.", cut.Markup, StringComparison.Ordinal);

        cut.FindAll("[data-testid=play-fact] button")[0].Click();

        Assert.Contains("Старый дом.", cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("Жил призрак.", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void Handout_row_shows_the_player_text_in_two_lines_and_a_short_show_button()
    {
        var scenario = new ScenarioDto
        {
            Id = ScenarioId, Name = "Дом",
            Handouts = [new(Guid.NewGuid(), 0, "Письмо тётушки", "Дорогой племянник", "выдать ночью", null, null)],
        };
        var cut = Render<PlayFactsPanel>(p => p.Add(c => c.Scenario, scenario).Add(c => c.ShowHref, id => $"play?handout={id}"));

        var button = cut.Find("[data-testid=show-handout]");
        Assert.Equal("Показать", button.TextContent.Trim());
        // Ссылка, а не кнопка: средний клик открывает показ в новой вкладке.
        Assert.Equal("A", button.TagName);
        Assert.Equal($"play?handout={scenario.Handouts[0].Id}", button.GetAttribute("href"));
        Assert.True(button.HasAttribute("data-replace"));
        Assert.Equal("Показать игрокам: Письмо тётушки", button.GetAttribute("aria-label"));
        Assert.Contains("Хранителю: выдать ночью", cut.Markup, StringComparison.Ordinal);
    }

    private static readonly Guid HouseId = Guid.Parse("0199b000-0000-7000-8000-000000000210");

    private IRenderedComponent<ScenarioPlay> Play(IReadOnlyList<ScenarioRunDto> runs, IReadOnlyList<InvestigatorDto>? party = null)
    {
        Services.AddSingleton(Fake.Of<IScenariosApi>(new()
        {
            [nameof(IScenariosApi.ListRunsAsync)] = _ => Task.FromResult(runs),
        }));
        Services.AddSingleton(Fake.Of<ICharactersApi>(new()
        {
            [nameof(ICharactersApi.GetCampaignInvestigatorsAsync)] = _ => Task.FromResult(party ?? []),
        }));
        Services.AddSingleton<ICatalogApi<SkillDto>>(new TableFakes.Skills());
        Services.AddSingleton(Fake.Of<ICatalogApi<MusicTrackDto>>(new()));
        AddAuthorization().SetAuthorized("Хранитель").SetPolicies(Policies.Keeper);
        var scenario = new ScenarioDto
        {
            Id = ScenarioId, Name = "Дом", CanEdit = true,
            Locations =
            [
                new()
                {
                    Id = HouseId, Name = "Дом", Description = "Тёмный дом.",
                    Checks = [new() { Id = Guid.NewGuid(), LocationId = HouseId, TargetKind = CheckTarget.Skill, SkillId = TableFakes.SpotHidden.Id, SkillName = "Внимание" }],
                },
                new() { Id = Guid.NewGuid(), ParentId = HouseId, Name = "Подвал" },
            ],
        };
        return Render<ScenarioPlay>(p => p.Add(c => c.Scenario, scenario).Add(c => c.LocationId, HouseId));
    }

    [Fact]
    public void Play_mode_shows_one_run_as_a_label_not_a_list()
    {
        var cut = Play([Run(ScenarioRunStatus.Running)]);

        cut.WaitForAssertion(() => Assert.Contains("Маски", cut.Find("[data-testid=play-run]").TextContent));
        Assert.Empty(cut.FindAll("select[data-testid=play-run]"));
        Assert.NotEmpty(cut.FindAll("[data-testid=play-combat]"));
    }

    [Fact]
    public void Play_mode_lists_runs_by_campaign_name_when_there_are_several()
    {
        var other = Run(ScenarioRunStatus.Running, campaign: "Тени") with { Id = Guid.NewGuid() };
        var cut = Play([Run(ScenarioRunStatus.Running), other]);

        cut.WaitForAssertion(() => Assert.Equal(["Маски", "Тени"], cut.FindAll("select[data-testid=play-run] option").Select(o => o.TextContent.Trim())));
    }

    [Fact]
    public void Play_mode_without_an_unfinished_run_says_so_instead_of_selecting_a_finished_one()
    {
        var cut = Play([Run(ScenarioRunStatus.Finished)]);

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid=play-no-runs]")));
        Assert.Empty(cut.FindAll("[data-testid=play-run]"));
        Assert.Empty(cut.FindAll("[data-testid=play-combat]"));
    }

    [Fact]
    public void Play_mode_preselects_the_only_investigator_of_the_run()
    {
        var only = new InvestigatorDto(Guid.NewGuid(), "Харви Уолтерс", "Аня", TableFakes.Sheet("Харви Уолтерс", 65));
        var cut = Play([Run(ScenarioRunStatus.Running)], [only]);
        cut.WaitForAssertion(() => Assert.Contains("Маски", cut.Find("[data-testid=play-run]").TextContent));

        cut.Find("[data-testid=run-check]").Click();

        Assert.Equal(only.CharacterId.ToString(), cut.Find("[data-testid=check-subject] option[selected]").GetAttribute("value"));
    }

    [Fact]
    public void Nested_locations_buttons_are_for_screens_without_the_tree_and_music_is_one_short_button()
    {
        var cut = Play([]);

        Assert.Contains("lg:hidden", cut.FindAll("[data-testid=play-location-detail] h3").Single(h => h.TextContent.Contains("Вложенные")).ParentElement!.ClassName, StringComparison.Ordinal);
        var music = cut.Find("[data-testid=location-music]");
        Assert.Equal("Музыка", music.TextContent.Trim());
        Assert.Equal("Настроить музыку локации", music.GetAttribute("aria-label"));
    }

    [Fact]
    public void Markdown_headings_are_listed_in_document_order_with_their_index()
    {
        var headings = MarkdownText.Headings("# Один\n\nтекст\n\n## Два\n\n### Три **жирный**\n\n#### Четыре");

        Assert.Equal([(1, "Один", 0), (2, "Два", 1), (3, "Три жирный", 2), (4, "Четыре", 3)], headings.Select(h => (h.Level, h.Text, h.Index)));
        Assert.Empty(MarkdownText.Headings(null));
    }

    [Fact]
    public void Text_tab_builds_contents_from_three_or_more_headings()
    {
        Services.AddSingleton(Fake.Of<IScenariosApi>(new()));
        var scenario = new ScenarioDto { Id = ScenarioId, Name = "Дом", BodyMd = "# А\n\nтекст\n\n## Б\n\nтекст\n\n## В\n\nтекст", CanEdit = true };

        var cut = Render<ScenarioTextTab>(p => p.Add(c => c.Scenario, scenario));
        Assert.Equal(["А", "Б", "В"], cut.FindAll("[data-testid=scenario-contents] button").Select(b => b.TextContent.Trim()));

        scenario.BodyMd = "# А\n\nтекст";
        cut.Render(p => p.Add(c => c.Scenario, scenario));
        Assert.Empty(cut.FindAll("[data-testid=scenario-contents]"));
    }

    [Fact]
    public async Task Import_adds_copy_to_a_taken_title_and_says_so()
    {
        var calls = new List<string?>();
        Services.AddSingleton(Fake.Of<IScenarioExchangeApi>(new()
        {
            [nameof(IScenarioExchangeApi.ImportAsync)] = args =>
            {
                calls.Add((string?)args![2]);
                return Task.FromResult(new ScenarioImportReport { DryRun = true, ScenarioName = "Дом" });
            },
        }));
        var cut = Render<ScenarioImportModal>(p => p.Add(m => m.Open, true).Add(m => m.ExistingNames, ["дом"]));
        Assert.Contains("Сначала выберите файл", cut.Markup, StringComparison.Ordinal);

        cut.FindComponent<InputFile>().UploadFiles(InputFileContent.CreateFromText("{}", "дом.json"));
        cut.WaitForAssertion(() => Assert.DoesNotContain("Сначала выберите файл", cut.Markup, StringComparison.Ordinal));
        await cut.Find("[data-testid=scenario-import-check]").ClickAsync();

        Assert.Equal(["", "Дом (копия)"], calls.Select(c => c ?? ""));
        Assert.Equal("Дом (копия)", cut.Find("[data-testid=scenario-import-name]").GetAttribute("value"));
        Assert.Contains("к названию добавлено «(копия)»", cut.Markup, StringComparison.Ordinal);
    }
}
