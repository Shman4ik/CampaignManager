using Bunit;
using CampaignManager.Contracts.Catalogs;
using CampaignManager.Contracts.Characters;
using CampaignManager.Contracts.Music;
using CampaignManager.Contracts.Scenarios;
using CampaignManager.Core;
using CampaignManager.Core.Catalogs;
using CampaignManager.Core.Characters;
using CampaignManager.Core.Scenarios;
using CampaignManager.UI.Catalogs;
using CampaignManager.UI.Characters;
using CampaignManager.UI.Scenarios;
using CampaignManager.UI.Shared;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CampaignManager.UI.Tests;

/// <summary>
/// Ревью интерфейса g4, волна 2 (F4b): вкладки рабочего места — локации, проверки, факты, раздатки, твари, предметы, НПС,
/// готовые сыщики и их окна. «Изменить» значком, один способ добавить, имя в подтверждении без кавычек, окно выбора с
/// полями с самого начала.
/// </summary>
public sealed class ScenarioReviewG4TabsTests : KitContext
{
    private static readonly Guid ScenarioId = Guid.Parse("0199b000-0000-7000-8000-000000000300");
    private static readonly Guid LocationId = Guid.Parse("0199b000-0000-7000-8000-000000000301");

    private static ScenarioCheckDto Check(CheckTarget target, Characteristic? characteristic = null, string? skill = null) => new()
    {
        Id = Guid.NewGuid(),
        LocationId = LocationId,
        TargetKind = target,
        Characteristic = characteristic,
        SkillName = skill,
        OnSuccess = "Находят следы.",
        OnFailure = "Теряют время.",
    };

    private static ScenarioDto Scenario(Action<ScenarioDto>? configure = null)
    {
        var scenario = new ScenarioDto { Id = ScenarioId, Name = "Дом", CanEdit = true };
        configure?.Invoke(scenario);
        return scenario;
    }

    private void AddApis()
    {
        Services.AddSingleton(Fake.Of<IScenariosApi>(new()));
        Services.AddSingleton(Fake.Of<ICharactersApi>(new()));
        Services.AddSingleton(Fake.Of<ICatalogApi<ItemDto>>(new()));
        Services.AddSingleton(Fake.Of<ICatalogApi<CreatureDto>>(new()));
        Services.AddSingleton(Fake.Of<ICatalogApi<SkillDto>>(new()
        {
            [nameof(ICatalogApi<SkillDto>.ListAsync)] = _ => Task.FromResult(new CatalogList<SkillDto>([], true)),
        }));
        Services.AddSingleton(Fake.Of<ICatalogApi<MusicTrackDto>>(new()
        {
            [nameof(ICatalogApi<MusicTrackDto>.ListAsync)] = _ => Task.FromResult(new CatalogList<MusicTrackDto>([], true)),
        }));
    }

    [Fact]
    public void Row_actions_are_a_labelled_pencil_and_a_menu_with_the_rows_own_items()
    {
        var cut = Render<PartRowActions>(p => p
            .Add(c => c.CanEdit, true)
            .Add(c => c.Name, "Библиотека")
            .Add(c => c.MenuItems, b =>
            {
                b.OpenComponent<RowMenuItem>(0);
                b.AddAttribute(1, nameof(RowMenuItem.ChildContent), (Microsoft.AspNetCore.Components.RenderFragment)(c => c.AddContent(0, "Изменить музыку")));
                b.CloseComponent();
            }));

        var edit = cut.Find("[data-testid=part-edit]");
        Assert.Equal("Изменить: Библиотека", edit.GetAttribute("aria-label"));
        Assert.DoesNotContain("Изменить", edit.TextContent, StringComparison.Ordinal); // значок без подписи
        Assert.Contains("min-h-8", edit.ClassName, StringComparison.Ordinal);

        cut.Find("[aria-label='Действия: Библиотека']").Click();
        var items = cut.FindAll("[role=menuitem]").Select(i => i.TextContent.Trim()).ToList();
        Assert.Equal(["Изменить музыку", "Удалить"], items);
    }

    [Fact]
    public void Locations_tab_moves_music_to_the_row_menu_and_drops_the_second_checks_link()
    {
        AddApis();
        var scenario = Scenario(s => s.Locations = [new() { Id = LocationId, Name = "Дом", Description = "Старый дом.", Checks = [Check(CheckTarget.Luck)] }]);
        var cut = Render<ScenarioLocationsTab>(p => p.Add(t => t.Scenario, scenario));

        cut.Find("[data-testid=location-row] button[aria-expanded]").Click();

        Assert.DoesNotContain("К проверкам", cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("Настроить музыку", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("Добавить вложенную локацию", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("Открыть в режиме игры", cut.Markup, StringComparison.Ordinal);
        // «Исходы проверок» — жирным подзаголовком, а не капсом секции.
        Assert.DoesNotContain("cm-section-title", cut.Markup, StringComparison.Ordinal);

        cut.Find("[aria-label='Действия: Дом']").Click();
        Assert.Contains(cut.FindAll("[role=menuitem]"), i => i.TextContent.Contains("Изменить музыку", StringComparison.Ordinal));
    }

    [Fact]
    public void Checks_tab_names_the_characteristic_in_full_has_no_luck_badge_and_one_add_button_on_top()
    {
        AddApis();
        var scenario = Scenario(s => s.Locations =
        [
            new()
            {
                Id = LocationId, Name = "Дом",
                Checks = [Check(CheckTarget.Luck), Check(CheckTarget.Characteristic, Characteristic.STR), Check(CheckTarget.Skill, skill: "Слух")],
            },
        ]);
        var cut = Render<ScenarioChecksTab>(p => p.Add(t => t.Scenario, scenario));

        var rows = cut.FindAll("[data-testid=check-row]");
        Assert.DoesNotContain("бросок Удачи", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("Сила", rows[1].TextContent, StringComparison.Ordinal);
        Assert.DoesNotContain("СИЛ", rows[1].TextContent, StringComparison.Ordinal);
        Assert.Contains("характеристика", rows[1].TextContent, StringComparison.Ordinal);

        // Подписанная кнопка «Добавить проверку» — одна; у группы только значок с именем для чтеца.
        Assert.Single(cut.FindAll("button"), b => b.TextContent.Contains("Добавить проверку", StringComparison.Ordinal));
        Assert.Equal("Добавить проверку в локацию: Дом", cut.Find("[data-testid=add-check-here]").GetAttribute("aria-label"));
    }

    [Fact]
    public void Check_form_shows_a_known_location_as_a_line_and_asks_for_it_only_when_added_from_the_top()
    {
        AddApis();
        ScenarioLocationDto[] locations = [new() { Id = LocationId, Name = "Дом" }];

        var fromGroup = Render<CheckFormModal>(p => p
            .Add(m => m.Open, true).Add(m => m.ScenarioId, ScenarioId).Add(m => m.Locations, locations)
            .Add(m => m.DefaultLocationId, LocationId).Add(m => m.LocationFixed, true));
        Assert.Empty(fromGroup.FindAll("[data-testid=check-location]"));
        Assert.Contains("Локация: Дом", fromGroup.Find("form").TextContent, StringComparison.Ordinal);

        var fromTop = Render<CheckFormModal>(p => p
            .Add(m => m.Open, true).Add(m => m.ScenarioId, ScenarioId).Add(m => m.Locations, locations)
            .Add(m => m.DefaultLocationId, LocationId));
        Assert.NotEmpty(fromTop.FindAll("[data-testid=check-location]"));

        // «Что проверяют» — тот же сегментный переключатель, что у сложности.
        Assert.Equal(3, fromTop.FindAll("[role=radiogroup][aria-label='Что проверяют'] [role=radio]").Count);
        Assert.NotEmpty(fromTop.FindAll("[role=radiogroup][aria-label='Сложность проверки']"));
    }

    [Fact]
    public void Facts_tab_shows_the_kind_as_a_grey_label_and_the_empty_line_without_instructions()
    {
        AddApis();
        var empty = Render<ScenarioFactsTab>(p => p.Add(t => t.Scenario, Scenario()));
        Assert.Contains("Нет фактов.", empty.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("справа", empty.Markup, StringComparison.Ordinal);

        var scenario = Scenario(s => s.KeyFacts = [new(Guid.NewGuid(), 0, KeyFactType.Truth, "Что в лесу", "Гла'аки.")]);
        var cut = Render<ScenarioFactsTab>(p => p.Add(t => t.Scenario, scenario));
        Assert.Empty(cut.FindAll(".cm-badge"));
        Assert.Contains("Зловещая истина", cut.Find("[data-testid=fact-kind]").TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public void Handouts_tab_puts_show_next_to_the_name_and_asks_for_a_thumbnail_not_the_original()
    {
        AddApis();
        var scenario = Scenario(s => s.Handouts = [new(Guid.NewGuid(), 0, "Письмо", "Дорогой друг…", null, Guid.NewGuid(), "/api/v1/files/письмо.png")]);
        var cut = Render<ScenarioHandoutsTab>(p => p.Add(t => t.Scenario, scenario));

        Assert.Equal("/api/v1/files/письмо.png?w=160", cut.Find("img").GetAttribute("src"));
        var show = cut.Find("[data-testid=handout-show]");
        Assert.Contains("cm-btn-secondary", show.ClassName, StringComparison.Ordinal);
        Assert.Equal("Изменить: Письмо", cut.Find("[data-testid=part-edit]").GetAttribute("aria-label"));
    }

    [Fact]
    public void Creatures_tab_marks_an_own_card_inside_the_row_not_with_a_badge_and_without_bare_hit_points()
    {
        AddApis();
        var scenario = Scenario(s => s.Creatures = [new() { Id = Guid.NewGuid(), Name = "Гла'аки", HasOwnStatblock = true, Count = 2 }]);
        var cut = Render<ScenarioCreaturesTab>(p => p.Add(t => t.Scenario, scenario));

        Assert.DoesNotContain("статблок", cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("ПЗ ", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("× 2", cut.Markup, StringComparison.Ordinal);

        cut.Find("[data-testid=creature-row] button[aria-expanded]").Click();
        Assert.Contains("Карточка изменена для сценария.", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("Добавить тварь", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void Items_tab_has_one_add_button_that_opens_a_search_panel_and_a_new_name_opens_the_form()
    {
        Services.AddSingleton(Fake.Of<IScenariosApi>(new()));
        Services.AddSingleton(Fake.Of<ICatalogApi<ItemDto>>(new()
        {
            [nameof(ICatalogApi<ItemDto>.ListAsync)] = _ => Task.FromResult(new CatalogList<ItemDto>([new() { Id = Guid.NewGuid(), Name = "Лампа", Type = "Свет" }], true)),
        }));
        var scenario = Scenario(s => s.Items =
        [
            new() { Id = Guid.NewGuid(), Name = "Идол", Description = "Резной." },
            new() { Id = Guid.NewGuid(), ItemId = Guid.NewGuid(), Name = "Лампа", Type = "Свет" },
        ]);
        var cut = Render<ScenarioItemsTab>(p => p.Add(t => t.Scenario, scenario));

        Assert.DoesNotContain("реквизит", cut.Markup, StringComparison.OrdinalIgnoreCase);
        Assert.Single(cut.FindAll("button"), b => b.TextContent.Contains("Добавить", StringComparison.Ordinal));
        // Метка — только у предмета справочника; «свой» ничем не отмечен.
        var badges = cut.FindAll("[data-testid=item-row] .cm-badge").Select(b => b.TextContent.Trim()).ToList();
        Assert.Equal(["Свет"], badges);

        cut.Find("[data-testid=add-item]").Click();
        var input = cut.Find("[data-testid=catalog-search-input]");
        input.Input("Нечто новое");
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid=catalog-search-custom]")));

        cut.Find("[data-testid=catalog-search-custom]").Click();
        Assert.Equal("Нечто новое", cut.Find("[data-testid=item-name]").GetAttribute("value"));
        Assert.Empty(cut.FindAll("[data-testid=item-search]"));
    }

    [Fact]
    public void Npc_tab_has_one_add_button_and_the_window_holds_role_and_count_disabled_until_a_pick()
    {
        Services.AddSingleton(Fake.Of<IScenariosApi>(new()));
        Services.AddSingleton(Fake.Of<ICharactersApi>(new()
        {
            [nameof(ICharactersApi.ListAsync)] = _ => Task.FromResult<IReadOnlyList<CharacterSummaryDto>>(
                [new() { Id = Guid.NewGuid(), Kind = CharacterKind.Npc, Name = "Шериф", Occupation = "Закон" }]),
        }));
        var scenario = Scenario(s => s.Npcs =
        [
            new(new CharacterSummaryDto { Id = Guid.NewGuid(), Name = "Август", Occupation = "Служитель" }, NpcRole.Enemy, 1, "Спит днём."),
        ]);
        var cut = Render<ScenarioNpcsTab>(p => p.Add(t => t.Scenario, scenario));

        Assert.Single(cut.FindAll("button, a"), b => b.TextContent.Contains("НПС", StringComparison.Ordinal) && b.ClassName?.Contains("cm-btn") == true);
        // Роль — нейтральный бейдж со значком (красный — только ошибка), заметка — целиком.
        var role = Assert.Single(cut.FindAll("[data-testid=npc-cast] .cm-badge"));
        Assert.Contains("cm-badge-neutral", role.ClassName, StringComparison.Ordinal);
        Assert.Contains("Спит днём.", cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("line-clamp", cut.Find("[data-testid=npc-row]").InnerHtml, StringComparison.Ordinal);

        cut.Find("[data-testid=add-npc]").Click();
        cut.WaitForState(() => cut.FindAll("[data-testid=picker-row]").Count == 1);

        Assert.True(cut.Find("fieldset").HasAttribute("disabled"));
        Assert.Contains("Выберите НПС", cut.Find("[data-testid=modal-footer-hint]").TextContent, StringComparison.Ordinal);
        Assert.NotEmpty(cut.FindAll("[data-testid=quick-npc]"));
        Assert.Contains("Создать нового", cut.Find("[data-testid=picker-extra]").TextContent, StringComparison.Ordinal);

        cut.Find("[data-testid=picker-row]").Click();
        Assert.False(cut.Find("fieldset").HasAttribute("disabled"));
        Assert.False(cut.Find("[data-testid=picker-confirm]").HasAttribute("disabled"));
    }

    [Fact]
    public void Pregens_tab_uses_the_dictionary_words_and_one_add_button()
    {
        Services.AddSingleton(Fake.Of<IScenariosApi>(new()));
        Services.AddSingleton(Fake.Of<ICharactersApi>(new()));

        var empty = Render<ScenarioPregensTab>(p => p.Add(t => t.Scenario, Scenario()));
        Assert.Contains("Нет готовых сыщиков.", empty.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("прегенов", empty.Markup, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("брониру", empty.Markup, StringComparison.OrdinalIgnoreCase);
        Assert.Single(empty.FindAll("button, a"), b => b.TextContent.Contains("Добавить готового сыщика", StringComparison.Ordinal));

        var scenario = Scenario(s => s.Pregens =
        [
            new(new CharacterSummaryDto { Id = Guid.NewGuid(), Name = "Томас Кларк", ScenarioId = ScenarioId, ScenarioName = "Дом" }, IsReserved: true),
        ]);
        var cut = Render<ScenarioPregensTab>(p => p.Add(t => t.Scenario, scenario));
        Assert.Contains("Выбран игроком", cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("Сценарий «Дом»", cut.Markup, StringComparison.Ordinal); // метка повторяла страницу
    }

    [Fact]
    public void Removing_a_row_asks_with_the_name_bold_and_without_quotes()
    {
        AddApis();
        var dialogs = Services.GetRequiredService<DialogService>();
        var scenario = Scenario(s => s.KeyFacts = [new(Guid.NewGuid(), 0, KeyFactType.Backstory, "Что было", null)]);
        var cut = Render<ScenarioFactsTab>(p => p.Add(t => t.Scenario, scenario));

        cut.Find("[aria-label='Действия: Что было']").Click();
        cut.Find("[role=menuitem]").Click();

        Assert.NotNull(dialogs.Current);
        Assert.Equal("Удалить факт?", dialogs.Current!.Title);
        Assert.Equal("Что было", dialogs.Current.Subject);
        Assert.DoesNotContain('«', dialogs.Current.Title);
    }

    [Fact]
    public void Picker_without_fields_always_keeps_the_old_behaviour_and_the_reason_hint_goes_away_after_a_pick()
    {
        var cut = Render<CatalogPickerModal<string>>(p => p
            .Add(m => m.Open, true)
            .Add(m => m.Title, "Тварь")
            .Add(m => m.Load, _ => Task.FromResult<IReadOnlyList<string>>(["Гуль"]))
            .Add(m => m.Key, s => s)
            .Add(m => m.Name, s => s)
            .Add(m => m.SelectReason, "Выберите тварь")
            .Add(m => m.ChildContent, row => builder => builder.AddContent(0, $"Поля {row}")));

        Assert.DoesNotContain("Поля", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("Выберите тварь", cut.Find("[data-testid=modal-footer-hint]").TextContent, StringComparison.Ordinal);

        cut.Find("[data-testid=picker-row]").Click();
        Assert.Contains("Поля Гуль", cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("Выберите тварь", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void Characteristic_target_is_named_in_full()
    {
        Assert.Equal("Мощь", ScenarioLabels.Target(Check(CheckTarget.Characteristic, Characteristic.POW)));
        Assert.Equal("Удача", ScenarioLabels.Target(Check(CheckTarget.Luck)));
    }
}
