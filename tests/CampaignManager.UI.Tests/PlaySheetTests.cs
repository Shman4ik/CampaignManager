using Bunit;
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
/// Режим «Игра» листа (решение владельца 2026-10-03): полоса состояния пишет лист теми же правилами, что «Состояние» и
/// «Рассудок» листа (урон — серьёзная рана, потеря ≥5 — проверка ИНТ), навыки — все, развитые отличаются от базовых,
/// только для чтения — управление выключено.
/// </summary>
public sealed class PlaySheetTests : KitContext
{
    private static readonly SkillDefinition Spot = new(Guid.NewGuid(), "Внимание")
    {
        Code = "skill.spot-hidden", BaseValue = 25, Category = SkillCategory.InformationGathering,
    };

    private static readonly SkillDefinition Listen = new(Guid.NewGuid(), "Слух")
    {
        Code = "skill.listen", BaseValue = 20, Category = SkillCategory.InformationGathering,
    };

    private static readonly SkillDefinition Swim = new(Guid.NewGuid(), "Плавание")
    {
        Code = "skill.swim", BaseValue = 15, Category = SkillCategory.Actions,
    };

    private static readonly SkillCatalog Catalog = new([Spot, Listen, Swim]);

    private readonly List<string?> _checks = [];

    public PlaySheetTests() => Services.AddSingleton(Fake.Of<IFilesApi>(new()));

    private static CharacterSheet Sheet() => new()
    {
        Characteristics = new Characteristics { Str = 50, Con = 50, Siz = 50, Dex = 50, App = 50, Int = 60, Pow = 50, Edu = 60 },
        Current = new CurrentValues { HitPoints = 10, MagicPoints = 10, Sanity = 50, Luck = 40 },
    };

    private IRenderedComponent<CascadingValue<SheetContext>> Render<TPanel>(CharacterSheet sheet, bool canEdit = true) where TPanel : IComponent
    {
        var context = new SheetContext(new CharacterDto { Sheet = sheet, CanEdit = canEdit }, Catalog, [], null!, () => { }, _checks.Add);
        return Render<CascadingValue<SheetContext>>(p => p.Add(c => c.Value, context).AddChildContent<TPanel>());
    }

    [Fact]
    public void Damage_of_one_attack_goes_through_wound_rules_and_shows_the_major_wound()
    {
        var sheet = Sheet();
        var cut = Render<PlayVitals>(sheet);

        cut.Find("[data-testid='play-damage']").Change("5");
        cut.Find("[data-testid='play-apply-damage']").Click();

        Assert.Equal(5, sheet.Current.HitPoints);
        Assert.True(sheet.Condition.MajorWound); // 5 из 10 — половина максимума
        Assert.Single(cut.FindAll("[data-testid='wound-major']"));
        Assert.Equal("true", cut.Find("[data-testid='play-flag-major-wound']").GetAttribute("aria-pressed"));
    }

    [Fact]
    public void Sanity_loss_of_five_is_one_cause_and_asks_for_the_int_check()
    {
        var sheet = Sheet();
        var cut = Render<PlayVitals>(sheet);

        cut.Find("[data-testid='play-sanity-amount']").Change("5");
        cut.Find("[data-testid='play-sanity-lose']").Click();

        Assert.Equal(45, sheet.Current.Sanity);
        Assert.Equal(5, sheet.Condition.LastSanityLoss);
        Assert.Equal(5, sheet.Condition.SanityLostToday);
        cut.Find("[data-testid='int-failure']").Click();
        Assert.Empty(cut.FindAll("[data-testid='int-check-prompt']"));
    }

    [Fact]
    public void Steps_stay_within_zero_and_maximum_and_plain_steps_do_not_count_as_sanity_loss()
    {
        var sheet = Sheet();
        sheet.Current.HitPoints = 1;
        var cut = Render<PlayVitals>(sheet);

        cut.Find("[data-testid='play-hp-minus']").Click();
        cut.Find("[data-testid='play-hp-minus']").Click();
        cut.Find("[data-testid='play-san-minus']").Click();

        Assert.Equal(0, sheet.Current.HitPoints);
        Assert.True(sheet.Condition.Unconscious); // 0 ПЗ без раны — без сознания (WoundRules)
        Assert.Equal(49, sheet.Current.Sanity);
        Assert.Equal(0, sheet.Condition.SanityLostToday);
    }

    [Fact]
    public void Clearing_dying_also_clears_stabilized_and_insanity_is_shown_but_not_toggled()
    {
        var sheet = Sheet();
        sheet.Condition.Dying = true;
        sheet.Condition.Stabilized = true;
        sheet.Condition.TemporaryInsanity = true;
        var cut = Render<PlayVitals>(sheet);

        cut.Find("[data-testid='play-flag-dying']").Click();

        Assert.False(sheet.Condition.Dying);
        Assert.False(sheet.Condition.Stabilized);
        var mind = cut.Find("[data-testid='play-mind']");
        Assert.Contains("Временное безумие", mind.TextContent);
        Assert.Empty(mind.QuerySelectorAll("button"));
    }

    [Fact]
    public void Read_only_sheet_keeps_the_numbers_and_the_bar_but_draws_no_controls()
    {
        var sheet = Sheet();
        sheet.Condition.MajorWound = true;
        sheet.Condition.TemporaryInsanity = true;
        var cut = Render<PlayVitals>(sheet, canEdit: false);

        Assert.Equal("10", cut.Find("[data-testid='play-hp-value']").TextContent);
        Assert.NotNull(cut.Find("[data-testid='play-hp'] [role='meter']"));
        Assert.Empty(cut.FindAll("[data-testid='play-hp-minus']"));
        Assert.Empty(cut.FindAll("[data-testid='play-damage']"));
        Assert.Empty(cut.FindAll("[data-testid='play-sanity-lose']"));
        Assert.Empty(cut.FindAll("[data-testid^='play-flag-']"));
        Assert.Empty(cut.FindAll("[data-testid='wound-major']"));
        // включённое — состоянием, а не кнопкой
        var states = cut.Find("[data-testid='play-states']");
        Assert.Contains("Серьёзная рана", states.TextContent);
        Assert.Contains("Временное безумие", states.TextContent);
        Assert.Empty(states.QuerySelectorAll("button"));
    }

    [Fact]
    public void Npc_has_no_luck_tile()
    {
        var context = new SheetContext(new CharacterDto { Sheet = Sheet(), CanEdit = true, Kind = CharacterKind.Npc }, Catalog, [], null!, () => { }, _checks.Add);
        var cut = Render<CascadingValue<SheetContext>>(p => p.Add(c => c.Value, context).AddChildContent<PlayVitals>());

        Assert.Empty(cut.FindAll("[data-testid='play-luck']"));
        Assert.NotNull(cut.Find("[data-testid='play-san']"));
    }

    [Fact]
    public async Task Marking_dead_asks_first_and_clearing_does_not()
    {
        var sheet = Sheet();
        var dialogs = Services.GetRequiredService<DialogService>();
        var cut = Render<PlayVitals>(sheet);

        await cut.InvokeAsync(() => cut.Find("[data-testid='play-flag-dead']").Click());
        Assert.False(sheet.Condition.Dead);
        Assert.Equal("Отметить сыщика мёртвым?", dialogs.Current?.Title);

        await cut.InvokeAsync(() => dialogs.Complete(false));
        await Task.Delay(100, Xunit.TestContext.Current.CancellationToken);
        Assert.False(sheet.Condition.Dead);

        await cut.InvokeAsync(() => cut.Find("[data-testid='play-flag-dead']").Click());
        await cut.InvokeAsync(() => dialogs.Complete(true));
        await Task.Delay(100, Xunit.TestContext.Current.CancellationToken);
        Assert.True(sheet.Condition.Dead);

        await cut.InvokeAsync(() => cut.Find("[data-testid='play-flag-dead']").Click()); // снять отметку — без вопроса
        Assert.False(sheet.Condition.Dead);
        Assert.Null(dialogs.Current);
    }

    [Fact]
    public void Dying_shows_one_line_alert_until_the_character_is_stabilized()
    {
        var sheet = Sheet();
        sheet.Condition.Dying = true;
        var cut = Render<PlayVitals>(sheet);

        Assert.Contains("При смерти", cut.Find("[data-testid='wound-dying']").TextContent);
        // справочник теста без этих навыков: кнопок проверки нет, тревога остаётся
        Assert.Empty(cut.FindAll("[data-testid='check-first-aid']"));

        sheet.Condition.Stabilized = true;
        var stabilized = Render<PlayVitals>(sheet);
        Assert.Empty(stabilized.FindAll("[data-testid='wound-dying']"));
    }

    [Fact]
    public void Int_check_prompt_has_the_check_first_and_two_equal_outcomes()
    {
        var sheet = Sheet();
        sheet.Condition.LastSanityLoss = 6;
        var cut = Render<PlayVitals>(sheet);

        var buttons = cut.FindAll("[data-testid='int-check-prompt'] button");
        Assert.Equal(["Проверка ИНТ", "Успех — безумие", "Провал — в своём уме"], buttons.Select(b => b.TextContent.Trim()));
        Assert.DoesNotContain(buttons, b => b.ClassName?.Contains("cm-btn-primary") == true);

        buttons[0].Click();
        Assert.Equal("char:INT", Assert.Single(_checks));
    }

    [Fact]
    public void Skills_start_with_the_frequent_block_then_the_alphabet_and_mute_only_small_base_values()
    {
        var sheet = Sheet();
        sheet.Skills.Add(new SheetSkill { SkillId = Spot.Id, Value = 60, Checked = true });
        sheet.Skills.Add(new SheetSkill { SkillId = Listen.Id, Value = 20 }); // на базе, но не меньше 20 — не приглушается
        var cut = Render<PlaySkills>(sheet);

        // «Частые» (по коду) первым блоком, остальное по алфавиту; Плавание без строки листа — на базе справочника
        Assert.Equal(["Внимание", "Слух"], cut.FindAll("[data-testid='play-skills-frequent'] .play-skill-name").Select(e => e.TextContent));
        Assert.Equal(["Плавание"], cut.FindAll("[data-testid='play-skills-others'] .play-skill-name").Select(e => e.TextContent));
        Assert.Equal(["Плавание"], cut.FindAll(".play-skill-base .play-skill-name").Select(e => e.TextContent));
        Assert.Single(cut.FindAll(".play-skill-tick i")); // отметка развития видна
        Assert.Equal("значение · ½ · ⅕", cut.Find("[data-testid='play-skill-legend']").TextContent);

        cut.Find("[data-testid='play-skill-search']").Input("плав");
        var swim = Assert.Single(cut.FindAll(".play-skill"));
        Assert.Contains("play-skill-base", swim.ClassName);

        swim.Click();
        Assert.Equal($"skill:{Swim.Id}", Assert.Single(_checks));
    }

    [Fact]
    public void Weapon_line_uses_units_and_hides_garbage_attacks()
    {
        var sheet = Sheet();
        sheet.Weapons.Add(new SheetWeapon
        {
            Name = "Арбалет", SkillId = Swim.Id, Damage = "1D8+2", Range = "50 метров", Attacks = "1/123456789", Ammo = "", Malfunction = "96",
        });
        sheet.Weapons.Add(new SheetWeapon { Name = "Револьвер", SkillId = Swim.Id, Damage = "1d10", Range = "15", Attacks = "1 (3)", Ammo = "6", Malfunction = "00" });
        sheet.Weapons.Add(new SheetWeapon { Name = "Булавка", Damage = "1d3" });
        var cut = Render<PlayWeapons>(sheet);

        var rows = cut.FindAll("[data-testid='play-weapon'] .play-row-meta").Select(e => e.TextContent.Trim()).ToList();
        Assert.Equal("1d8 + 2 · 50 м · осечка 96–100", rows[0]);
        Assert.Equal("1d10 · 15 м · атаки 1 (3) · патроны 6 · осечка 100", rows[1]);
        Assert.Equal("1d3", rows[2]);
        // оружие без навыка: в справочнике теста «драки» нет, поэтому подписи нет (с дракой — см. следующий тест)
        Assert.Empty(cut.FindAll("[data-testid='play-weapon-fallback']"));
    }

    [Fact]
    public void Weapon_without_a_skill_is_checked_by_brawl_and_says_so()
    {
        var brawl = new SkillDefinition(Guid.NewGuid(), "Ближний бой (драка)") { Code = "skill.fighting.brawl", BaseValue = 25, Category = SkillCategory.CombatGeneral };
        var catalog = new SkillCatalog([Spot, brawl]);
        var sheet = Sheet();
        sheet.Weapons.Add(new SheetWeapon { Name = "Булавка", Damage = "1d3" });
        var context = new SheetContext(new CharacterDto { Sheet = sheet, CanEdit = true }, catalog, [], null!, () => { }, _checks.Add);
        var cut = Render<CascadingValue<SheetContext>>(p => p.Add(c => c.Value, context).AddChildContent<PlayWeapons>());

        Assert.Contains("Ближний бой (драка)", cut.Find("[data-testid='play-weapon-fallback']").TextContent);
        cut.Find("[data-testid='play-weapon-check']").Click();
        Assert.Equal($"skill:{brawl.Id}", Assert.Single(_checks));
    }

    [Fact]
    public void Spell_row_expands_its_description_and_normalizes_the_cost()
    {
        var sheet = Sheet();
        sheet.Spells.Add(new SheetSpell { Name = "Затуманить память", Cost = "1d6 магии, 1d2 Рассудка", Description = "Стирает воспоминания.", CastingTime = "1 раунд" });
        sheet.Spells.Add(new SheetSpell { Name = "Без описания", Cost = "5 МОЩ" });
        var cut = Render<PlayExtras>(sheet);

        Assert.Equal("1d6 ПМ, 1d2 рассудка", cut.Find("[data-testid='play-spell-toggle'] .play-row-meta").TextContent.Trim());
        Assert.Empty(cut.FindAll("[data-testid='play-spell-detail']"));

        cut.Find("[data-testid='play-spell-toggle']").Click();

        var detail = cut.Find("[data-testid='play-spell-detail']");
        Assert.Contains("Стирает воспоминания.", detail.TextContent);
        Assert.Contains("1 раунд", detail.TextContent);
        Assert.Single(cut.FindAll("[data-testid='play-spell-toggle']")); // вторая строка без описания — не кнопка
    }

    [Fact]
    public void Gear_is_plain_text_not_pills_and_notes_write_the_biography_field()
    {
        var sheet = Sheet();
        sheet.Equipment.Add(new EquipmentItem { Name = "Фонарь" });
        sheet.Equipment.Add(new EquipmentItem { Name = "Нож" });
        var gear = Render<PlayExtras>(sheet);
        Assert.Equal("Фонарь · Нож", System.Text.RegularExpressions.Regex.Replace(gear.Find(".play-gear").TextContent, @"\s+", " ").Trim());
        Assert.Empty(gear.FindAll("li"));

        var changes = 0;
        var context = new SheetContext(new CharacterDto { Sheet = sheet, CanEdit = true }, Catalog, [], null!, () => changes++, _checks.Add);
        var notes = Render<CascadingValue<SheetContext>>(p => p.Add(c => c.Value, context).AddChildContent<PlayNotes>());
        notes.Find("[data-testid='play-notes-field']").Change("Ключ у дворецкого");

        Assert.Equal("Ключ у дворецкого", sheet.Biography.Notes);
        Assert.Equal(1, changes);
    }

    [Fact]
    public void Read_only_notes_are_hidden_when_empty()
    {
        Assert.Empty(Render<PlayNotes>(Sheet(), canEdit: false).FindAll("[data-testid='play-notes']"));
    }
}
