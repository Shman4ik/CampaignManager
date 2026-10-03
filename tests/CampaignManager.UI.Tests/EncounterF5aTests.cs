using System.Text.RegularExpressions;
using Bunit;
using CampaignManager.Contracts.Encounters;
using CampaignManager.Contracts.Scenarios;
using CampaignManager.Core.Catalogs;
using CampaignManager.Core.Characters;
using CampaignManager.Core.Encounters;
using CampaignManager.UI.Encounters;
using CampaignManager.UI.Encounters.Combat;
using CampaignManager.UI.Platform;
using CampaignManager.UI.Shared;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CampaignManager.UI.Tests;

/// <summary>
/// Обход бой-экрана (F5a, отчёт g5): журнал заголовком, итогом и «Подробнее», без счётчиков; цели по сторонам; окно участника
/// (поиск и тип по вкладке, сторона, «Добавлен»); тонкая полоса рассудка; расстановка; раны без «Выздоровления»; пункты-галочки.
/// </summary>
public sealed class EncounterF5aTests : KitContext
{
    private static readonly DateTimeOffset Now = new(2026, 10, 3, 12, 0, 0, TimeSpan.Zero);

    public EncounterF5aTests()
    {
        // Окно участника умеет сценарии и прохождения — без них вкладки источников не отрисовать.
        Services.AddSingleton(Fake.Of<IScenariosApi>(new()
        {
            [nameof(IScenariosApi.ListAsync)] = _ => Task.FromResult(new ScenarioListDto(_scenarios, true)),
        }));
        Services.AddSingleton(Fake.Of<IRunsApi>(new()));
    }

    private IReadOnlyList<ScenarioSummaryDto> _scenarios = [];

    private static string Flat(string text) => Regex.Replace(text, @"\s+", " ").Trim();

    // ───────── журнал (B30, B35)

    [Fact]
    public void Log_entry_shows_title_one_summary_line_and_details_without_counters()
    {
        var entry = new EncounterLogEntry
        {
            Kind = EncounterLogKind.Attack,
            Round = 1,
            Text = "Бесформенное отродье попадает. Цель: Адам Урбан-Фокс (Ближний бой). Урон 8.",
            Lines =
            [
                "Бесформенное отродье (Ближний бой): 30 против 60 — трудный успех",
                "Урон: 2d6 = 4, бонус к урону +4 → 8.",
                "Бесформенное отродье: Атак за раунд, 0 → 1",
                "Адам Урбан-Фокс: Защит за раунд, 0 → 1",
                "Адам Урбан-Фокс: Урон 8, 3 → 0 (Ближний бой: 2d6; серьёзная рана, при смерти)",
            ],
            At = Now,
        };

        var cut = Render<EncounterLog>(p => p.Add(c => c.Entries, [entry]));

        Assert.Equal("Адам Урбан-Фокс: Урон 8, 3 → 0", cut.Find("[data-testid=log-summary]").TextContent.Trim());
        var more = cut.Find("details.encounter-more");
        Assert.Contains("Подробнее (2)", Flat(more.TextContent), StringComparison.Ordinal);
        Assert.DoesNotContain("за раунд", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void Log_entry_without_effects_has_neither_summary_nor_details_and_joined_label_is_not_doubled()
    {
        var entry = new EncounterLogEntry { Kind = EncounterLogKind.Joined, Round = 0, Text = "Вступили: Август, Зомби.", At = Now };

        var cut = Render<EncounterLog>(p => p.Add(c => c.Entries, [entry]));

        Assert.Empty(cut.FindAll("[data-testid=log-summary]"));
        Assert.Empty(cut.FindAll("details"));
        Assert.Single(Regex.Matches(cut.Markup, "Вступили"));
    }

    // ───────── цели по сторонам (B21, B33)

    private static EncounterState Mixed()
    {
        var state = new EncounterState();
        EncounterEngine.Add(state, Fighter("Артур", EncounterSide.Investigators), Now);
        EncounterEngine.Add(state, Fighter("Зомби", EncounterSide.Enemies), Now);
        EncounterEngine.Add(state, Fighter("Джон", EncounterSide.Neutral), Now);
        EncounterEngine.Add(state, Fighter("Элизабет", EncounterSide.Investigators), Now);
        return state;
    }

    private static EncounterParticipant Fighter(string name, EncounterSide side) =>
        new() { Name = name, SourceName = name, Kind = ParticipantKind.Npc, Side = side, HitPoints = 10, MaxHitPoints = 10, Stats = new ParticipantStats { Dex = 50 } };

    [Fact]
    public void Target_select_puts_opponents_first_neutral_next_and_own_side_last()
    {
        var state = Mixed();
        var artur = state.Participants.Single(p => p.Name == "Артур");

        var cut = Render<ParticipantSelect>(p => p
            .Add(c => c.Participants, [.. state.Participants.Where(x => x.Id != artur.Id)])
            .Add(c => c.Grouped, true)
            .Add(c => c.Perspective, artur));

        var groups = cut.FindAll("optgroup").Select(g => g.GetAttribute("label")).ToList();
        Assert.Equal(["Противники", "Нейтральные", "Свои"], groups);
        Assert.Equal(["Зомби"], cut.Find("optgroup[label=Противники]").QuerySelectorAll("option").Select(o => o.TextContent.Trim()));
    }

    [Fact]
    public void Actor_select_groups_by_side_and_single_side_stays_flat()
    {
        var state = Mixed();

        var grouped = Render<ParticipantSelect>(p => p.Add(c => c.Participants, state.Participants).Add(c => c.Grouped, true));
        Assert.Equal(["Сыщики", "Противники", "Нейтральные"], grouped.FindAll("optgroup").Select(g => g.GetAttribute("label")));

        var flat = Render<ParticipantSelect>(p => p.Add(c => c.Participants, [.. state.Participants.Where(x => x.Side == EncounterSide.Investigators)]).Add(c => c.Grouped, true));
        Assert.Empty(flat.FindAll("optgroup"));
    }

    [Fact]
    public void Target_chips_are_in_side_rows_and_all_works_per_side()
    {
        var state = Mixed();
        IReadOnlySet<Guid> selected = new HashSet<Guid>();

        var cut = Render<ParticipantSelect>(p => p
            .Add(c => c.Participants, state.Participants)
            .Add(c => c.Multiple, true)
            .Add(c => c.Grouped, true)
            .Add(c => c.Values, selected)
            .Add(c => c.ValuesChanged, values => selected = values));

        // «Все» — только у сторон, где больше одного: у сыщиков (Артур, Элизабет).
        var all = cut.FindAll("[data-testid=participant-all]");
        Assert.Single(all);
        all[0].Click();

        Assert.Equal(["Артур", "Элизабет"], state.Participants.Where(x => selected.Contains(x.Id)).Select(x => x.Name).Order());
    }

    // ───────── строка участника (B13, B14, B15)

    [Fact]
    public void Investigator_row_has_a_thin_sanity_bar_and_creature_row_does_not()
    {
        var investigator = Fighter("Артур", EncounterSide.Investigators);
        investigator.Sanity = 40;
        investigator.MaxSanity = 80;
        var creature = EncounterParticipants.FromStatblock(null, "Гуль", new Statblock { HitPoints = 13 });

        var withSanity = Render<ParticipantRow>(p => p.Add(c => c.Participant, investigator));
        var without = Render<ParticipantRow>(p => p.Add(c => c.Participant, creature));

        var bar = withSanity.Find("[data-testid=stat-san-thin]");
        Assert.Equal("40", bar.GetAttribute("aria-valuenow"));
        Assert.Contains("width: 50%", bar.InnerHtml, StringComparison.Ordinal);
        Assert.Empty(without.FindAll("[data-testid=stat-san-thin]"));
        // Цифры рассудка — по касанию имени, как и раньше.
        Assert.Empty(withSanity.FindAll("[data-testid=stat-san]"));
        withSanity.Find("button.encounter-row-name").Click();
        Assert.Single(withSanity.FindAll("[data-testid=stat-san]"));
    }

    // ───────── пункты меню (B16, B18)

    [Fact]
    public void Rule_menu_item_is_a_checkbox_with_a_check_and_sheet_item_is_a_link()
    {
        var cut = Render<RowMenu>(p => p
            .Add(c => c.AriaLabel, "Действия сцены")
            .Add(c => c.ChildContent, (RenderFragment)(b =>
            {
                b.OpenComponent<RowMenuItem>(0);
                b.AddAttribute(1, nameof(RowMenuItem.Checked), true);
                b.AddAttribute(2, nameof(RowMenuItem.ChildContent), (RenderFragment)(c => c.AddContent(0, "Нокаут манёвром")));
                b.CloseComponent();
                b.OpenComponent<RowMenuItem>(3);
                b.AddAttribute(4, nameof(RowMenuItem.Checked), false);
                b.AddAttribute(5, nameof(RowMenuItem.ChildContent), (RenderFragment)(c => c.AddContent(0, "Удача против обморока")));
                b.CloseComponent();
                b.OpenComponent<RowMenuItem>(6);
                b.AddAttribute(7, nameof(RowMenuItem.Href), "character/1");
                b.AddAttribute(8, nameof(RowMenuItem.Target), "_blank");
                b.AddAttribute(9, nameof(RowMenuItem.ChildContent), (RenderFragment)(c => c.AddContent(0, "Открыть лист")));
                b.CloseComponent();
            })));

        cut.Find("button[aria-haspopup=menu]").Click();

        var boxes = cut.FindAll("[role=menuitemcheckbox]");
        Assert.Equal(["true", "false"], boxes.Select(x => x.GetAttribute("aria-checked")));
        Assert.Contains("text-accent-700", boxes[0].InnerHtml, StringComparison.Ordinal);
        Assert.Contains("opacity-0", boxes[1].InnerHtml, StringComparison.Ordinal);
        var link = cut.Find("a[role=menuitem]");
        Assert.Equal("character/1", link.GetAttribute("href"));
        Assert.Equal("_blank", link.GetAttribute("target"));
        Assert.Contains("noopener", link.GetAttribute("rel"), StringComparison.Ordinal);
    }

    // ───────── панели боя

    private EncounterSession SessionOf(EncounterState state)
    {
        var session = new EncounterSession(new CombatPanelTests.SavingEncounters(), new EncounterSheetSync(new CombatPanelTests.NoCharacters()),
            Services.GetRequiredService<BrowserStorage>(), Time);
        session.Start(new EncounterDto { Id = Guid.CreateVersion7(), State = state, Version = 1 }, new SkillCatalog([]));
        return session;
    }

    private IRenderedComponent<CascadingValue<EncounterSession>> InSession<TPanel>(EncounterSession session) where TPanel : IComponent =>
        Render<CascadingValue<EncounterSession>>(p => p.Add(c => c.Value, session).Add(c => c.IsFixed, true).AddChildContent<TPanel>());

    [Fact]
    public void Wounds_tab_picks_the_patient_with_a_segment_and_has_no_recovery_between_games()
    {
        var state = Mixed();
        var hurt = state.Participants.Single(p => p.Name == "Артур");
        hurt.HitPoints = 4;
        hurt.Profile.FirstAid = 60;
        EncounterQueue.Start(state, Now);

        var cut = InSession<WoundsPanel>(SessionOf(state));

        var list = cut.Find("[data-testid=wounded-list]");
        Assert.Equal("radiogroup", list.GetAttribute("role"));
        Assert.Contains("ПЗ 4/10", list.TextContent, StringComparison.Ordinal);
        Assert.Contains("cm-segment", list.InnerHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("Выздоровление", cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("Неделя лечения", cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("стр.", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void Wounds_tab_badge_says_what_it_counts()
    {
        var state = Mixed();
        var dying = state.Participants.Single(p => p.Name == "Зомби");
        dying.HitPoints = 0;
        dying.Dying = true;
        EncounterQueue.Start(state, Now);

        var cut = InSession<CombatPanel>(SessionOf(state));

        var tab = cut.FindAll("[role=tab]").Single(t => t.TextContent.Contains("Раны", StringComparison.Ordinal));
        Assert.Contains("при смерти", tab.TextContent, StringComparison.Ordinal);
        Assert.Contains("fa-heart-crack", tab.InnerHtml, StringComparison.Ordinal);
    }

    [Fact]
    public void Setup_column_shows_firearm_toggles_and_the_initiative_block_only_when_there_is_something_to_decide()
    {
        var state = Mixed();
        var cut = InSession<CombatSetup>(SessionOf(state));
        Assert.Empty(cut.FindAll("[data-testid=combat-setup]"));

        var gunner = state.Participants.Single(p => p.Name == "Артур");
        gunner.Profile.Attacks = [new CombatAttack { Key = "colt", Name = "Кольт", Skill = 50, Damage = "1d10", AmmoCapacity = 7 }];
        state.Combat.InitiativeRolls = true;
        cut = InSession<CombatSetup>(SessionOf(state));

        var chip = Assert.Single(cut.FindAll("[data-testid=firearm-ready]"));
        Assert.Equal("false", chip.GetAttribute("aria-pressed"));
        Assert.Single(cut.FindAll("[data-testid=initiative-block]"));
        chip.Click();
        Assert.True(gunner.Combat.FirearmReady);
    }

    [Fact]
    public void Sanity_tab_does_not_default_to_the_dying_investigator_when_someone_else_can_check()
    {
        var state = Mixed();
        var dying = state.Participants.Single(p => p.Name == "Артур");
        dying.Sanity = 50;
        dying.HitPoints = 0;
        dying.Dying = true;
        var fit = state.Participants.Single(p => p.Name == "Элизабет");
        fit.Sanity = 60;
        // Ход у твари: рассудка у неё нет, значит «кто видит» выбирается из тех, у кого он есть.
        var zombie = state.Participants.Single(p => p.Name == "Зомби");
        EncounterQueue.Start(state, Now);
        state.ActiveParticipantId = zombie.Id;

        var cut = InSession<CombatSanityPanel>(SessionOf(state));

        Assert.Equal("Элизабет", cut.Find("select[aria-label='Кто проходит проверку'] option[selected]").TextContent.Trim());
        Assert.Contains("Проверить рассудок", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void Maneuver_choice_is_a_segment_not_a_primary_button()
    {
        var cut = InSession<ManeuverPanel>(SessionOf(Mixed()));

        var group = cut.FindAll("[role=radiogroup]").Single(g => g.GetAttribute("aria-label") == "Манёвр");
        Assert.All(group.QuerySelectorAll("button"), b => Assert.Equal("radio", b.GetAttribute("role")));
        // Главная кнопка на вкладке одна — «Провести манёвр».
        Assert.Single(cut.FindAll("button.cm-btn-primary"));
    }

    [Fact]
    public void Attack_button_stands_above_the_extras_and_dodge_is_not_an_attack_option()
    {
        var state = Mixed();
        EncounterQueue.Start(state, Now);

        var cut = InSession<AttackPanel>(SessionOf(state));

        var markup = cut.Markup;
        Assert.True(markup.IndexOf("attack-resolve", StringComparison.Ordinal) < markup.IndexOf("attack-extra", StringComparison.Ordinal),
            "«Провести атаку» должна стоять над «Дополнительно».");
        // Навыки-пункты — под подписью группы, без повтора «: оружие…» в каждом.
        Assert.DoesNotContain("%: оружие…", markup, StringComparison.Ordinal);
    }

    [Fact]
    public void Preview_hides_counters_and_does_not_repeat_the_title_in_a_note()
    {
        var state = new EncounterState();
        var ghoul = EncounterParticipants.FromStatblock(null, "Гуль", new Statblock { HitPoints = 13 });
        EncounterEngine.Add(state, ghoul, Now);
        var resolution = new EncounterResolution
        {
            Title = "Выстрел. Урон 8, серьёзная рана, падает, нужна проверка ВЫН.",
            Effects = [new EncounterEffect { Kind = EncounterEffectKind.Damage, ParticipantId = ghoul.Id, Amount = 8 }],
        };

        var cut = Render<ResolutionPreview>(p => p.Add(c => c.State, state).Add(c => c.Resolution, resolution));

        // Все части пояснения уже есть в заголовке — второй раз их не пишем.
        Assert.Empty(cut.FindAll("tbody .cm-meta"));
        Assert.DoesNotContain("Чем чревато", cut.Markup, StringComparison.Ordinal);
    }

    // ───────── окно участника (B7–B12)

    private sealed class StubSource(string key, string label, params ParticipantOption[] options) : IParticipantSource
    {
        public string Key => key;

        public bool UsesScenario { get; init; }

        public string Label => label;

        public string Icon => "fa-user";

        public Task<ParticipantSourceResult> LoadAsync(ParticipantPickerContext context, CancellationToken cancellationToken) =>
            Task.FromResult(new ParticipantSourceResult(options));
    }

    private static ParticipantOption Option(string name, ParticipantKind kind, string? category = null, Guid? sheet = null) =>
        new($"{kind}:{name}", name, "ПЗ 10", kind, sheet, AllowCount: false,
            (_, _) => Task.FromResult<IReadOnlyList<EncounterParticipant>>(
                [new EncounterParticipant { Name = name, SourceName = name, Kind = kind, Side = kind == ParticipantKind.Creature ? EncounterSide.Enemies : EncounterSide.Neutral, SourceCharacterId = sheet }]))
        {
            Category = category,
        };

    private IRenderedComponent<ParticipantPicker> OpenPicker(Action<IReadOnlyList<EncounterParticipant>>? picked = null, IReadOnlySet<Guid>? used = null) =>
        Render<ParticipantPicker>(p => p
            .Add(c => c.Open, true)
            .Add(c => c.Sources,
            [
                new StubSource("npcs", "НПС", Option("Август", ParticipantKind.Npc), Option("Алистер", ParticipantKind.Npc)),
                new StubSource("bestiary", "Бестиарий",
                    Option("Глубоководный", ParticipantKind.Creature, "Монстры"), Option("Шоггот", ParticipantKind.Creature, "Монстры Мифов"), Option("Крыса", ParticipantKind.Creature, "Животные")),
            ])
            .Add(c => c.Context, new ParticipantPickerContext(null, new SkillCatalog([])))
            .Add(c => c.UsedCharacterIds, used ?? new HashSet<Guid>())
            .Add(c => c.OnPicked, list => picked?.Invoke(list)));

    private static void Tab(IRenderedComponent<ParticipantPicker> cut, string label) =>
        cut.FindAll("dialog [role=tab], [role=tab]").First(t => t.TextContent.Contains(label, StringComparison.Ordinal)).Click();

    [Fact]
    public void Picker_search_resets_with_the_tab_and_empty_result_says_so()
    {
        var cut = OpenPicker();
        Tab(cut, "Бестиарий");
        cut.Find("[data-testid=picker-search]").Input("ццц");

        Assert.Contains("Нет участников по этим условиям.", cut.Markup, StringComparison.Ordinal);

        Tab(cut, "НПС");

        Assert.Equal("", cut.Find("[data-testid=picker-search]").GetAttribute("value") ?? "");
        Assert.DoesNotContain("Нет участников", cut.Markup, StringComparison.Ordinal);
        Assert.Equal(2, cut.FindAll("[data-testid=picker-add]").Count);
    }

    [Fact]
    public void Picker_filters_the_bestiary_by_type_chips()
    {
        var cut = OpenPicker();
        Tab(cut, "Бестиарий");

        var chips = cut.Find("[data-testid=picker-category]").QuerySelectorAll("button").Select(b => b.TextContent.Trim()).ToList();
        Assert.Equal(["Любой тип", "Монстры", "Монстры Мифов", "Животные"], chips);
        cut.Find("[data-testid=picker-category]").QuerySelectorAll("button").Single(b => b.TextContent.Contains("Животные", StringComparison.Ordinal)).Click();

        Assert.Single(cut.FindAll("[data-testid=picker-add]"));
        Assert.Contains("Крыса", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void Picker_adds_npcs_neutral_and_creatures_hostile_by_default_and_remembers_the_chosen_side()
    {
        IReadOnlyList<EncounterParticipant> picked = [];
        var cut = OpenPicker(list => picked = list);

        Tab(cut, "НПС");
        cut.FindAll("[data-testid=picker-add]")[0].Click();
        Assert.Equal(EncounterSide.Neutral, picked.Single().Side);

        // «Противники» у НПС: тот, кого добавили, сразу противник; выбор запоминается для вкладки.
        cut.Find("[data-testid=picker-side]").QuerySelectorAll("button").Single(b => b.TextContent.Contains("Противники", StringComparison.Ordinal)).Click();
        cut.FindAll("[data-testid=picker-add]")[1].Click();
        Assert.Equal(EncounterSide.Enemies, picked.Single().Side);

        Tab(cut, "Бестиарий");
        cut.FindAll("[data-testid=picker-add]")[0].Click();
        Assert.Equal(EncounterSide.Enemies, picked.Single().Side);
        cut.Find("[data-testid=picker-side]").QuerySelectorAll("button").Single(b => b.TextContent.Contains("Свои", StringComparison.Ordinal)).Click();
        cut.FindAll("[data-testid=picker-add]")[1].Click();
        Assert.Equal(EncounterSide.Investigators, picked.Single().Side);
    }

    [Fact]
    public void Picker_marks_a_sheet_already_in_the_scene_with_a_disabled_button_of_the_same_size()
    {
        var sheet = Guid.NewGuid();
        var cut = Render<ParticipantPicker>(p => p
            .Add(c => c.Open, true)
            .Add(c => c.Sources, [new StubSource("npcs", "НПС", Option("Август", ParticipantKind.Npc, sheet: sheet), Option("Алистер", ParticipantKind.Npc))])
            .Add(c => c.Context, new ParticipantPickerContext(null, new SkillCatalog([])))
            .Add(c => c.UsedCharacterIds, new HashSet<Guid> { sheet }));

        var disabled = cut.FindAll("button[disabled]").Single(b => b.TextContent.Contains("Добавлен", StringComparison.Ordinal));
        Assert.Contains("cm-btn-sm", disabled.ClassName, StringComparison.Ordinal);
        Assert.Single(cut.FindAll("[data-testid=picker-add]"));
        // «Готово» — главная кнопка окна.
        Assert.Contains("cm-btn-primary", cut.FindAll("dialog button").Single(b => b.TextContent.Trim() == "Готово").ClassName, StringComparison.Ordinal);
    }

    [Fact]
    public void Picker_tells_two_scenarios_with_one_title_apart_and_leaves_unique_titles_alone()
    {
        _scenarios =
        [
            new ScenarioSummaryDto { Id = Guid.NewGuid(), Name = "Эликсир жизни", AuthorName = "Анна", UpdatedAt = new DateTimeOffset(2026, 4, 12, 12, 0, 0, TimeSpan.Zero) },
            new ScenarioSummaryDto { Id = Guid.NewGuid(), Name = "Эликсир жизни", UpdatedAt = new DateTimeOffset(2026, 9, 1, 12, 0, 0, TimeSpan.Zero) },
            new ScenarioSummaryDto { Id = Guid.NewGuid(), Name = "Туман" },
        ];

        var cut = Render<ParticipantPicker>(p => p
            .Add(c => c.Open, true)
            .Add(c => c.Sources, [new StubSource("scenario", "Сценарий") { UsesScenario = true }])
            .Add(c => c.Context, new ParticipantPickerContext(null, new SkillCatalog([]))));

        var options = cut.Find("[data-testid=picker-scenario]").QuerySelectorAll("option").Skip(1).Select(o => o.TextContent.Trim()).ToList();
        Assert.Equal(3, options.Count);
        Assert.Single(options, o => o.Contains("Анна", StringComparison.Ordinal));
        Assert.Equal(2, options.Where(o => o.StartsWith("Эликсир жизни ·", StringComparison.Ordinal)).Distinct().Count());
        Assert.Contains("Туман", options);
    }
}
