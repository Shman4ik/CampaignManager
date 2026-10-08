using Bunit;
using CampaignManager.Contracts.Catalogs;
using CampaignManager.Contracts.Characters;
using CampaignManager.Contracts.Files;
using CampaignManager.Core.Catalogs;
using CampaignManager.Core.Characters;
using CampaignManager.UI.Characters;
using CampaignManager.UI.Shared;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CampaignManager.UI.Tests;

/// <summary>
/// Лист сыщика, режим «Лист», по ревью g2 (F2b): фаза развития — мастер по шагам, шаги без дела не рисуются, завершение с
/// неприменёнными шагами спрашивает; урон и цена заклинания приводятся к записи правил; заметка о знакомом открывается
/// касанием имени; фобия и мания — одна кнопка и сегмент без номеров таблиц.
/// </summary>
public sealed class SheetReviewG2bTests : KitContext
{
    private static readonly SkillDefinition Spot = new(Guid.NewGuid(), "Внимание")
    {
        Code = "skill.spot-hidden", BaseValue = 25, Category = SkillCategory.InformationGathering,
    };

    private static readonly SkillCatalog Catalog = new([Spot]);

    public SheetReviewG2bTests() => Services.AddSingleton(Fake.Of<IFilesApi>(new()));

    private IRenderedComponent<CascadingValue<SheetContext>> Render<TPanel>(CharacterSheet sheet, Action<ComponentParameterCollectionBuilder<TPanel>>? parameters = null)
        where TPanel : IComponent
    {
        var context = new SheetContext(new CharacterDto { Sheet = sheet, CanEdit = true }, Catalog, [], null!, () => { }, _ => { });
        return Render<CascadingValue<SheetContext>>(p => p.Add(c => c.Value, context)
            .AddChildContent<TPanel>(parameters));
    }

    private static CharacterSheet WithMark() => new()
    {
        Current = new CurrentValues { Sanity = 50 },
        Skills = [new SheetSkill { SkillId = Spot.Id, Value = 60, Checked = true }],
    };

    [Fact]
    public void Development_phase_has_no_experience_step_without_marks_and_no_empty_steps()
    {
        var cut = Render<DevelopmentPhaseModal>(new CharacterSheet(), p => p.Add(m => m.Open, true));

        // Удача, награда, Средства: без отметок, без биографии для самолечения и без видов привыкания других шагов нет.
        Assert.Contains("Шаг 1 из 3 · Восстановление Удачи", cut.Find("[data-testid='dev-step-title']").TextContent);
        Assert.Empty(cut.FindAll("[data-testid='dev-skill']"));
    }

    [Fact]
    public void Development_phase_shows_one_step_at_a_time_and_walks_with_next_and_back()
    {
        var cut = Render<DevelopmentPhaseModal>(WithMark(), p => p.Add(m => m.Open, true));

        Assert.Contains("Шаг 1 из 4 · Проверки опыта", cut.Find("[data-testid='dev-step-title']").TextContent);
        Assert.Single(cut.FindAll("[data-testid='dev-skill']"));
        Assert.Empty(cut.FindAll("[data-testid='dev-finish']")); // «Стереть отметки и завершить» — только на последнем шаге
        Assert.Empty(cut.FindAll("[data-testid='dev-back']"));

        cut.Find("[data-testid='dev-next']").Click();
        Assert.Contains("Шаг 2 из 4 · Восстановление Удачи", cut.Find("[data-testid='dev-step-title']").TextContent);
        Assert.Empty(cut.FindAll("[data-testid='dev-skill']"));

        cut.Find("[data-testid='dev-back']").Click();
        Assert.Contains("Проверки опыта", cut.Find("[data-testid='dev-step-title']").TextContent);
    }

    [Fact]
    public async Task Finishing_with_unapplied_steps_asks_first_and_keeps_the_marks_until_confirmed()
    {
        var sheet = WithMark();
        var cut = Render<DevelopmentPhaseModal>(sheet, p => p.Add(m => m.Open, true));
        var dialogs = Services.GetRequiredService<DialogService>();

        cut.Find("[data-testid='dev-step-4']").Click();
        cut.Find("[data-testid='dev-finish']").Click();

        var request = Assert.IsType<ConfirmRequest>(dialogs.Current);
        Assert.Contains("Проверки опыта: не брошено 1 из 1", request.Message);
        Assert.Contains("Восстановление Удачи", request.Message);
        Assert.True(sheet.Skills.Single().Checked);

        dialogs.Complete(false);
        await Task.Yield();
        Assert.True(sheet.Skills.Single().Checked);
    }

    [Fact]
    public void Finishing_without_marks_and_with_every_step_done_does_not_ask()
    {
        var sheet = new CharacterSheet { Current = new CurrentValues { Sanity = 50, Luck = 40 } };
        var cut = Render<DevelopmentPhaseModal>(sheet, p => p.Add(m => m.Open, true));
        var dialogs = Services.GetRequiredService<DialogService>();

        // Удача → бросок вписан; награда → ручная прибавка; Средства → «Применить»
        Dice.Enqueue(1, 1, 1);
        cut.Find("[data-testid='dev-luck']").Click();
        cut.Find("[data-testid='dev-next']").Click();
        cut.FindAll("button").First(b => b.TextContent.Trim() == "Прибавить").Click();
        cut.Find("[data-testid='dev-next']").Click();
        cut.FindAll("button").First(b => b.TextContent.Trim() == "Применить").Click();

        cut.Find("[data-testid='dev-finish']").Click();

        Assert.Null(dialogs.Current);
    }

    /// <summary>
    /// Средства — один раз за фазу, деньги — тоже один раз (стр. 94): раньше каждое «Применить» снова прибавляло наличные, а
    /// пересчёт затирал «дом в Аркхеме» числом таблицы даже без смены достатка.
    /// </summary>
    [Fact]
    public void Credit_step_applies_once_and_adds_cash_once_keeping_assets_of_the_same_tier()
    {
        var sheet = new CharacterSheet
        {
            Current = new CurrentValues { Sanity = 50, Luck = 40 },
            Finances = new Finances { Cash = 80, Assets = "дом в Аркхеме", PocketMoney = 10 },
        };
        var cut = Render<DevelopmentPhaseModal>(sheet, p => p.Add(m => m.Open, true));
        cut.Find("[data-testid='dev-step-3']").Click();
        Assert.Contains("Средства и занятия", cut.Find("[data-testid='dev-step-title']").TextContent);

        Button("Применить").Click();
        Assert.True(Button("Применить").HasAttribute("disabled"));

        Button("Пересчитать деньги").Click();
        var cash = sheet.Finances.Cash;
        Assert.True(cash > 80);
        Assert.True(Button("Пересчитать деньги").HasAttribute("disabled"));
        Assert.Equal("дом в Аркхеме", sheet.Finances.Assets);
        Assert.Equal(10m, sheet.Finances.PocketMoney);

        AngleSharp.Dom.IElement Button(string text) => cut.FindAll("button").First(b => b.TextContent.Trim() == text);
    }

    /// <summary>«Я богат!» бросают снова, пока достаток не догонит активы; другие варианты после него — нет.</summary>
    [Fact]
    public void Rich_is_rolled_again_until_money_is_recalculated()
    {
        var sheet = new CharacterSheet { Current = new CurrentValues { Sanity = 50, Luck = 40 } };
        var cut = Render<DevelopmentPhaseModal>(sheet, p => p.Add(m => m.Open, true));
        cut.Find("[data-testid='dev-step-3']").Click();

        cut.FindAll("input[name='credit-change']")[0].Change(true); // «Я богат!»
        Dice.Enqueue(4);
        Button("Применить").Click();
        Dice.Enqueue(3);
        Button("Бросить ещё").Click();
        Assert.False(Button("Бросить ещё").HasAttribute("disabled"));

        cut.FindAll("input[name='credit-change']")[1].Change(true); // «Дела идут на лад» после «Я богат!» — уже нет
        Assert.True(Button("Бросить ещё").HasAttribute("disabled"));

        Button("Пересчитать деньги").Click();
        cut.FindAll("input[name='credit-change']")[0].Change(true);
        Assert.True(Button("Бросить ещё").HasAttribute("disabled"));

        AngleSharp.Dom.IElement Button(string text) => cut.FindAll("button").First(b => b.TextContent.Trim() == text);
    }

    [Fact]
    public void Weapon_damage_is_shown_and_stored_in_the_dice_notation_of_the_rules()
    {
        var sheet = new CharacterSheet { Weapons = [new SheetWeapon { Name = "Револьвер", Damage = "1D8+2" }] };
        var cut = Render<WeaponsPanel>(sheet);

        var input = cut.Find("[data-testid='weapon-damage']");
        Assert.Equal("1d8 + 2", input.GetAttribute("value"));

        input.Change("1D4+БКУ");

        Assert.Equal("1d4 + бонус к урону", sheet.Weapons.Single().Damage);
    }

    [Fact]
    public void Weapon_note_is_a_link_until_there_is_a_note()
    {
        var sheet = new CharacterSheet { Weapons = [new SheetWeapon { Name = "Нож" }, new SheetWeapon { Name = "Топор", Notes = "Тупой" }] };
        var cut = Render<WeaponsPanel>(sheet);

        Assert.Single(cut.FindAll("button[aria-label^='Добавить заметку']"));
        Assert.Single(cut.FindAll("input[aria-label$=': заметки']"));

        cut.Find("button[aria-label='Добавить заметку: Нож']").Click();

        Assert.Equal(2, cut.FindAll("input[aria-label$=': заметки']").Count);
    }

    [Fact]
    public void Spell_cost_uses_one_unit_for_magic_points()
    {
        var sheet = new CharacterSheet { Spells = [new SheetSpell { Name = "Затуманить память", Cost = "1D6 магии, 1d2 Рассудка" }] };
        var cut = Render<SpellsPanel>(sheet);

        Assert.Equal("1d6 ПМ, 1d2 рассудка", cut.Find("[data-testid='spell-cost']").GetAttribute("value"));
    }

    [Fact]
    public void Equipment_has_column_headers_and_finances_have_one_hint_line_without_era()
    {
        var sheet = new CharacterSheet { Equipment = [new EquipmentItem { Name = "Фонарь" }] };
        var equipment = Render<EquipmentPanel>(sheet);
        Assert.Contains("Предмет", equipment.Find("[aria-hidden='true'].sm\\:grid").TextContent);
        Assert.Contains("Описание", equipment.Find("[aria-hidden='true'].sm\\:grid").TextContent);

        var finances = Render<FinancesPanel>(new CharacterSheet());
        var hint = finances.Find("[data-testid='finances-hint']").TextContent;
        Assert.DoesNotContain("1920", hint);
        Assert.DoesNotContain("таблиц", hint, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("наличные", hint);
    }

    [Fact]
    public void Fellow_note_field_opens_on_a_tap_on_the_name_and_a_note_shows_as_text()
    {
        var withNote = Guid.NewGuid();
        var without = Guid.NewGuid();
        Services.AddSingleton(Fake.Of<ICharactersApi>(new()
        {
            [nameof(ICharactersApi.GetPartyAsync)] = _ => Task.FromResult<IReadOnlyList<PartyMemberDto>>(
            [
                new(withNote, "Адам Урбан-Фокс", "Решала", "Alena"),
                new(without, "Генри Пирс", null, null),
            ]),
        }));
        var sheet = new CharacterSheet
        {
            FellowInvestigators = [new FellowInvestigator { CharacterId = withNote, Name = "Адам Урбан-Фокс", Note = "Доверяю наполовину" }],
        };
        var cut = Render<FellowInvestigatorsPanel>(sheet);
        cut.WaitForState(() => cut.FindAll("[data-testid='fellow-row']").Count == 2);

        Assert.Empty(cut.FindAll("input[placeholder='Что сыщик думает о нём']"));
        Assert.Contains("Доверяю наполовину", cut.Markup);

        cut.FindAll("[data-testid='fellow-toggle']")[1].Click();
        var input = cut.Find("input[placeholder='Что сыщик думает о нём']");
        input.Change("Подозрительный");

        Assert.Equal("Подозрительный", sheet.FellowInvestigators.Single(f => f.CharacterId == without).Note);
    }

    [Fact]
    public void Phobia_and_mania_share_one_add_button_and_a_segment_without_table_numbers()
    {
        var sheet = new CharacterSheet();
        var cut = Render<SanityPanel>(sheet);

        Assert.Single(cut.FindAll("[data-testid='add-condition']"));
        Assert.Empty(cut.FindAll("[data-testid='add-phobia']"));
        Assert.Empty(cut.FindAll("[data-testid='add-mania']"));

        cut.Find("[data-testid='add-condition']").Click();

        var dialog = cut.Find("dialog");
        Assert.Contains("Новая фобия", dialog.TextContent);
        Assert.DoesNotContain("табл", dialog.TextContent, StringComparison.OrdinalIgnoreCase);
        var segments = cut.FindAll("dialog [role='radio']").Select(s => s.TextContent.Trim()).ToList();
        Assert.Equal(["Фобия", "Мания"], segments);
    }

    [Fact]
    public void Phobia_table_row_is_found_by_search_and_fills_the_name()
    {
        var sheet = new CharacterSheet();
        var cut = Render<SanityPanel>(sheet);
        cut.Find("[data-testid='add-condition']").Click();

        cut.Find("dialog [data-testid='catalog-search-input']").Input("Арахно");
        cut.Find("dialog .catalog-search-row").Click();

        Assert.Contains("Арахнофобия", cut.Find("[data-testid='condition-name']").GetAttribute("value"));
        cut.Find("[data-testid='condition-save']").Click();
        Assert.Equal("Арахнофобия", Assert.Single(sheet.InsanityConditions).Name);
    }

    [Fact]
    public void Book_row_keeps_numbers_and_removal_in_the_menu_and_names_units_in_full()
    {
        var sheet = new CharacterSheet
        {
            MythosBooks = [new MythosBookRecord { Name = "Азатот и другие", Language = "Английский", SanityLoss = "1D4", MythosInitial = 1, MythosFull = 3, MythosRating = 12 }],
        };
        var cut = Render<MythosBooksPanel>(sheet);

        var stats = cut.Find("[data-testid='mythos-book-row']").TextContent;
        Assert.Contains("Потеря Рассудка 1d4", stats);
        Assert.Contains("Мифы: чтение +1, изучение +3", stats);
        Assert.DoesNotContain("РАС", stats);
        Assert.DoesNotContain("ЗМ", stats);
        Assert.Empty(cut.FindAll("[data-testid='mythos-book-edit']"));
        Assert.Contains("Добавить книгу", cut.Markup);
    }

    [Fact]
    public void Habituation_empty_state_is_one_plain_line_and_the_add_button_is_a_verb_phrase()
    {
        var cut = Render<HabituationPanel>(new CharacterSheet());

        Assert.Contains("Нет видов тварей.", cut.Markup);
        Assert.Contains("Добавить вид тварей", cut.Find("[data-testid='habituation-add']").TextContent);
    }

    [Fact]
    public void Skill_row_keeps_name_and_base_together_and_explains_the_adjacent_bonus_on_tap()
    {
        var line = new SkillLine("k", "Стрельба (винтовка)", 40, 25) { SpecializationBonus = 10 };
        var cut = Render<SkillRow>(p => p.Add(r => r.Line, line));

        Assert.NotNull(cut.Find(".skill-name-line .skill-base"));
        Assert.DoesNotContain("смежной специализации этого", cut.Markup);

        cut.Find(".cm-pop-trigger").Click();
        Assert.Contains("Смежная специализация", cut.Find(".cm-pop-panel").TextContent);
    }
}
