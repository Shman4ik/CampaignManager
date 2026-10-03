using Bunit;
using Bunit.TestDoubles;
using CampaignManager.Contracts.Campaigns;
using CampaignManager.Contracts.Catalogs;
using CampaignManager.Contracts.Characters;
using CampaignManager.Core;
using CampaignManager.Contracts.Identity;
using CampaignManager.Core.Characters;
using CampaignManager.Core.Scenarios;
using CampaignManager.UI.Characters;
using CampaignManager.UI.Characters.Creation;
using CampaignManager.UI.Characters.Library;
using CampaignManager.UI.Scenarios;
using CampaignManager.UI.Shared;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using CampaignManager.Contracts.Scenarios;

namespace CampaignManager.UI.Tests;

/// <summary>
/// Библиотека НПС, быстрый НПС и общий поиск по справочнику (ревью g3, F3b): шапка карточки — ссылка, главное действие — в подвале,
/// строка «ПЗ · боевой навык», роль в чипе сценария; поиск: пустой запрос, «Не найдено», стирание, без своей «Отмены» в окне;
/// окно «Занять в сценарии»: фокус на сценарии, причина у выключенной кнопки; страница: «Только архив», склонение, сброс фильтров.
/// </summary>
public sealed class UxF3bTests : KitContext
{
    private static CharacterSummaryDto Npc(string name, Action<CharacterSummaryDto>? with = null)
    {
        var dto = new CharacterSummaryDto
        {
            Id = Guid.NewGuid(), Kind = CharacterKind.Npc, Status = CharacterStatus.Active, Name = name, CanEdit = true,
            Occupation = "Бандит", Age = 32, Gender = "мужской", Residence = "Бостон", HitPoints = 11,
            CombatSkill = "Ближний бой (драка)", CombatValue = 40, Backstory = "Работает на Тёрнера.",
        };
        with?.Invoke(dto);
        return dto;
    }

    // ── Карточка ────────────────────────────────────────────────────────────

    [Fact]
    public void Card_header_is_the_link_and_there_is_no_open_button()
    {
        var npc = Npc("Август");

        var cut = Render<CharacterSummaryCard>(p => p.Add(c => c.Character, npc).Add(c => c.OnCast, EventCallback.Factory.Create<CharacterSummaryDto>(this, _ => { })));

        Assert.Equal($"character/{npc.Id}", cut.Find("[data-testid=character-link]").GetAttribute("href"));
        Assert.DoesNotContain(cut.FindAll("button"), b => b.TextContent.Trim() == "Открыть");
        Assert.Contains("Занять в сценарии", cut.Find("[data-testid=summary-cast-action]").TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public void Card_shows_hit_points_and_best_combat_skill_but_neither_gender_nor_residence()
    {
        var cut = Render<CharacterSummaryCard>(p => p.Add(c => c.Character, Npc("Август")));

        Assert.Equal("ПЗ 11 · Ближний бой (драка) 40%", cut.Find("[data-testid=summary-stats]").TextContent.Trim());
        Assert.Equal("Бандит · 32 года", cut.Find("[data-testid=summary-who]").TextContent.Trim());
        Assert.DoesNotContain("Мужской", cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("Бостон", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void Card_without_combat_skill_shows_hit_points_only_and_a_blank_sheet_shows_no_stats()
    {
        var withHp = Render<CharacterSummaryCard>(p => p.Add(c => c.Character, Npc("Нора", d => d.CombatSkill = null)));
        Assert.Equal("ПЗ 11", withHp.Find("[data-testid=summary-stats]").TextContent.Trim());

        var blank = Render<CharacterSummaryCard>(p => p.Add(c => c.Character, Npc("Пустой", d => { d.CombatSkill = null; d.HitPoints = 0; })));
        Assert.Empty(blank.FindAll("[data-testid=summary-stats]"));
    }

    [Fact]
    public void Card_cast_chip_names_the_scenario_the_role_and_the_count()
    {
        var npc = Npc("Август", d => d.Casts =
        [
            new("Дом с привидением", NpcRole.Enemy, 3),
            new("Среди древних деревьев", NpcRole.Neutral, 1),
        ]);

        var cut = Render<CharacterSummaryCard>(p => p.Add(c => c.Character, npc));

        var chips = cut.FindAll("[data-testid=summary-cast]").Select(c => c.TextContent.Trim()).ToList();
        Assert.Equal(["Дом с привидением · враг ×3", "Среди древних деревьев · нейтрал"], chips);
    }

    [Fact]
    public void Archived_card_offers_restore_as_its_main_action_and_cast_does_nothing()
    {
        var restored = new List<Guid>();
        var npc = Npc("Фрэнк", d => d.Status = CharacterStatus.Archived);

        var cut = Render<CharacterSummaryCard>(p => p
            .Add(c => c.Character, npc)
            .Add(c => c.OnCast, EventCallback.Factory.Create<CharacterSummaryDto>(this, _ => { }))
            .Add(c => c.OnRestore, EventCallback.Factory.Create<CharacterSummaryDto>(this, c => restored.Add(c.Id))));

        Assert.Empty(cut.FindAll("[data-testid=summary-cast-action]"));
        cut.Find("[data-testid=summary-restore-action]").Click();
        Assert.Equal([npc.Id], restored);
    }

    // ── Поиск по справочнику ────────────────────────────────────────────────

    private IRenderedComponent<CatalogSearch<string>> Search(Action<ComponentParameterCollectionBuilder<CatalogSearch<string>>>? more = null) =>
        Render<CatalogSearch<string>>(p =>
        {
            p.Add(c => c.Load, () => Task.FromResult<IReadOnlyList<string>>(["Нож", "Кольт", "Дубинка", "Винчестер", "Топор", "Лук", "Арбалет", "Рапира", "Кортик", "Булава", "Праща", "Кистень"]))
                .Add(c => c.Name, s => s);
            more?.Invoke(p);
        });

    [Fact]
    public void Search_shows_the_first_records_on_an_empty_query()
    {
        var cut = Search();

        cut.WaitForAssertion(() => Assert.Equal(10, cut.FindAll(".catalog-search-row").Count));
    }

    [Fact]
    public void Search_can_stay_silent_on_an_empty_query()
    {
        var cut = Search(p => p.Add(c => c.ShowOnEmpty, false));

        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[data-testid=catalog-search-input]")));
        Assert.Empty(cut.FindAll(".catalog-search-row"));
        Assert.Empty(cut.FindAll("[data-testid=catalog-search-empty]"));
    }

    [Fact]
    public void Search_without_matches_says_so_instead_of_an_empty_panel()
    {
        var cut = Search(p => p.Add(c => c.NotFound, "Оружие не найдено."));
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".catalog-search-row")));

        cut.Find("[data-testid=catalog-search-input]").Input("револьвер");

        Assert.Equal("Оружие не найдено.", cut.Find("[data-testid=catalog-search-empty]").TextContent.Trim());
        Assert.Empty(cut.FindAll(".catalog-search-results"));
    }

    [Fact]
    public void Search_with_a_custom_record_offers_it_instead_of_the_not_found_line()
    {
        var cut = Search(p => p.Add(c => c.OnCustom, EventCallback.Factory.Create<string>(this, _ => { })));
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".catalog-search-row")));

        cut.Find("[data-testid=catalog-search-input]").Input("револьвер");

        Assert.Empty(cut.FindAll("[data-testid=catalog-search-empty]"));
        Assert.Contains("Добавить «револьвер»", cut.Find("[data-testid=catalog-search-custom]").TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public void Search_clear_button_wipes_the_query_and_cancel_exists_only_when_asked_for()
    {
        var cancelled = 0;
        var cut = Search(p => p.Add(c => c.OnCancel, EventCallback.Factory.Create(this, () => cancelled++)));
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".catalog-search-row")));
        Assert.Empty(cut.FindAll("[data-testid=catalog-search-clear]"));

        cut.Find("[data-testid=catalog-search-input]").Input("нож");
        cut.Find("[data-testid=catalog-search-clear]").Click();

        Assert.Equal("", cut.Find("[data-testid=catalog-search-input]").GetAttribute("value") ?? "");
        cut.FindAll("button").Single(b => b.TextContent.Trim() == "Отмена").Click();
        Assert.Equal(1, cancelled);

        var inWindow = Search();
        inWindow.WaitForAssertion(() => Assert.NotEmpty(inWindow.FindAll(".catalog-search-row")));
        Assert.DoesNotContain("Отмена", inWindow.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void Compact_search_shows_five_rows_without_its_own_scroll()
    {
        var cut = Search(p => p.Add(c => c.Compact, true));

        cut.WaitForAssertion(() => Assert.Equal(5, cut.FindAll(".catalog-search-row").Count));
        Assert.Contains("catalog-search-compact", cut.Find(".catalog-search-results").ClassName, StringComparison.Ordinal);
    }

    // ── Окно «Занять в сценарии» ────────────────────────────────────────────

    private IRenderedComponent<CastNpcDialog> CastDialog(params ScenarioSummaryDto[] scenarios)
    {
        Services.AddSingleton(Fake.Of<IScenariosApi>(new()
        {
            [nameof(IScenariosApi.ListAsync)] = _ => Task.FromResult(new ScenarioListDto(scenarios, true)),
        }));
        return Render<CastNpcDialog>(p => p.Add(c => c.Open, true).Add(c => c.Character, Npc("Август")));
    }

    private static ScenarioSummaryDto Scenario(string name) => new() { Id = Guid.NewGuid(), Name = name, CanEdit = true };

    [Fact]
    public void Cast_dialog_asks_for_the_scenario_first_and_explains_the_disabled_button()
    {
        var cut = CastDialog(Scenario("Дом с привидением"));

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid=cast-scenario]")));
        var save = cut.Find("[data-testid=cast-save]");
        Assert.True(save.HasAttribute("disabled"));
        Assert.Equal("Занять", save.TextContent.Trim());
        Assert.Equal("Выберите сценарий", cut.Find("[data-testid=disabled-reason]").TextContent.Trim());
        Assert.Contains("Сколько человек", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void Cast_dialog_reports_scenario_role_and_count_to_the_page()
    {
        var scenario = Scenario("Дом с привидением");
        var saved = new List<NpcCastSaved>();
        Services.AddSingleton(Fake.Of<IScenariosApi>(new()
        {
            [nameof(IScenariosApi.ListAsync)] = _ => Task.FromResult(new ScenarioListDto([scenario], true)),
            [nameof(IScenariosApi.CastNpcAsync)] = _ => Task.CompletedTask,
        }));
        var cut = Render<CastNpcDialog>(p => p
            .Add(c => c.Open, true)
            .Add(c => c.Character, Npc("Август"))
            .Add(c => c.OnCastSaved, EventCallback.Factory.Create<NpcCastSaved>(this, s => saved.Add(s))));
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid=cast-scenario]")));

        cut.Find("[data-testid=cast-scenario]").Change(scenario.Id.ToString());
        cut.Find("[data-testid=cast-role]").Change(nameof(NpcRole.Enemy));
        cut.Find("[data-testid=cast-count]").Change("3");
        cut.Find("form").Submit();

        cut.WaitForAssertion(() => Assert.Single(saved));
        Assert.Equal(new NpcCastSaved(scenario.Id, "Дом с привидением", NpcRole.Enemy, 3), saved[0]);
    }

    // ── Быстрый НПС ─────────────────────────────────────────────────────────

    private IRenderedComponent<QuickNpcModal> Quick(List<(Guid Id, string Name)>? created = null)
    {
        Dice.Enqueue(0, 0, 0, 0);
        Services.AddSingleton(Fake.Of<ICatalogApi<SkillDto>>(new()
        {
            [nameof(ICatalogApi<SkillDto>.ListAsync)] = _ => Task.FromResult(new CatalogList<SkillDto>([], false)),
        }));
        Services.AddSingleton(Fake.Of<ICatalogApi<WeaponDto>>(new()
        {
            [nameof(ICatalogApi<WeaponDto>.ListAsync)] = _ => Task.FromResult(new CatalogList<WeaponDto>([], false)),
        }));
        Services.AddSingleton(Fake.Of<ICharactersApi>(new()
        {
            [nameof(ICharactersApi.CreateAsync)] = _ => Task.FromResult(new CharacterCreatedDto(Guid.NewGuid(), 1)),
        }));
        var cut = Render<QuickNpcModal>(p => p
            .Add(c => c.Open, true)
            .Add(c => c.OnCreatedNamed, EventCallback.Factory.Create<(Guid Id, string Name)>(this, c => created?.Add(c))));
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid=quick-name]")));
        return cut;
    }

    [Fact]
    public void Quick_npc_first_screen_has_archetype_name_level_and_weapon_and_the_rest_waits_in_details()
    {
        var cut = Quick();

        Assert.NotEmpty(cut.FindAll("[data-testid^=archetype-]"));
        Assert.Contains("Опытность в бою", cut.Markup, StringComparison.Ordinal);
        Assert.NotEmpty(cut.FindAll("[data-testid=catalog-search]"));
        Assert.Empty(cut.FindAll(".catalog-search-row"));                       // пустой запрос оружия — без списка
        Assert.False(cut.Find("[data-testid=quick-details]").HasAttribute("open"));
        Assert.DoesNotContain("гл. 10", cut.Markup, StringComparison.Ordinal);  // ссылок на главы книги в тексте нет
        Assert.Single(cut.FindAll("button"), b => b.TextContent.Trim() == "Отмена"); // одна «Отмена» — в подвале
    }

    [Fact]
    public void Quick_npc_creation_reports_the_name_so_the_library_can_stay_on_its_page()
    {
        var created = new List<(Guid Id, string Name)>();
        var cut = Quick(created);
        cut.Find("[data-testid=quick-name]").Change("  Фрэнк Мур ");

        cut.Find("form").Submit();

        cut.WaitForAssertion(() => Assert.Single(created));
        Assert.Equal("Фрэнк Мур", created[0].Name);
    }

    // ── Страница библиотеки ─────────────────────────────────────────────────

    private IRenderedComponent<NpcLibraryPage> Library(params CharacterSummaryDto[] items)
    {
        AddAuthorization().SetAuthorized("Хранитель").SetPolicies(Policies.Keeper);
        Services.AddSingleton(Fake.Of<ICharactersApi>(new()
        {
            [nameof(ICharactersApi.ListAsync)] = _ => Task.FromResult<IReadOnlyList<CharacterSummaryDto>>(items),
        }));
        Services.AddSingleton(Fake.Of<ICampaignsApi>(new()));
        Services.AddSingleton(Fake.Of<IScenariosApi>(new()));
        var cut = Render<NpcLibraryPage>();
        cut.WaitForState(() => cut.FindAll("[data-testid=library], [data-testid=empty-state]").Count > 0);
        return cut;
    }

    [Fact]
    public void Library_subtitle_declines_the_count_and_reset_is_disabled_without_filters()
    {
        var cut = Library(Npc("Август"), Npc("Нора"), Npc("Пол"));

        Assert.Contains("3 листа", cut.Find(".cm-topbar").TextContent, StringComparison.Ordinal);
        Assert.True(cut.FindAll("button").Where(b => b.TextContent.Contains("Сбросить фильтры", StringComparison.Ordinal)).All(b => b.HasAttribute("disabled")));

        cut.Find("[data-testid=npc-search]").Input("Нора");

        Assert.False(cut.FindAll("button").First(b => b.TextContent.Contains("Сбросить фильтры", StringComparison.Ordinal)).HasAttribute("disabled"));
    }

    [Fact]
    public void Library_has_only_archive_checkbox_and_the_place_select_does_not_disappear()
    {
        var cut = Library(Npc("Август"));

        Assert.Contains("Только архив", cut.Markup, StringComparison.Ordinal);
        var place = cut.Find("[data-testid=npc-place]");
        Assert.True(place.HasAttribute("disabled")); // мест нет — селект остаётся, неактивный
        Assert.Contains("Любое место", place.TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public void Library_empty_result_is_one_line_and_pages_hold_twelve_cards()
    {
        var many = Enumerable.Range(1, 30).Select(i => Npc($"НПС {i:00}")).ToArray();
        var cut = Library(many);

        Assert.Equal(12, cut.FindAll("[data-testid=character-summary]").Count);

        cut.Find("[data-testid=npc-search]").Input("zzzz");

        var empty = cut.Find("[data-testid=empty-state]");
        Assert.Equal("Нет НПС по этим условиям.", empty.TextContent.Trim());
        Assert.Contains("cm-empty-compact", empty.ClassName, StringComparison.Ordinal);
    }

    [Fact]
    public void Library_archive_confirmation_is_reversible_and_toast_names_the_object()
    {
        var npc = Npc("Август");
        var api = Fake.Of<ICharactersApi>(new()
        {
            [nameof(ICharactersApi.ListAsync)] = _ => Task.FromResult<IReadOnlyList<CharacterSummaryDto>>([npc]),
            [nameof(ICharactersApi.SetStatusAsync)] = _ => Task.FromResult(new CharacterSavedDto(1, DateTimeOffset.UnixEpoch)),
        });
        AddAuthorization().SetAuthorized("Хранитель").SetPolicies(Policies.Keeper);
        Services.AddSingleton(api);
        Services.AddSingleton(Fake.Of<ICampaignsApi>(new()));
        Services.AddSingleton(Fake.Of<IScenariosApi>(new()));
        var dialogs = Services.GetRequiredService<DialogService>();
        var cut = Render<NpcLibraryPage>();
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid=character-summary]")));

        cut.Find("[aria-label='Действия: Август']").Click();
        cut.Find("[role=menuitem]").Click();

        var request = dialogs.Current!;
        Assert.Equal("Вернуть можно через «Только архив».", request.Message);
        Assert.Equal(ButtonVariant.Primary, request.ConfirmVariant);
        Assert.Null(request.Details);

        cut.InvokeAsync(() => dialogs.Complete(true));
        var toasts = Services.GetRequiredService<ToastService>();
        cut.WaitForAssertion(() => Assert.Single(toasts.Messages));
        Assert.Equal("В архиве: Август", toasts.Messages[0].Message);
        Assert.Equal("Август", toasts.Messages[0].Subject);
    }
}
