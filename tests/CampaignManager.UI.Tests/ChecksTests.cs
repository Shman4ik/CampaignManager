using Bunit;
using CampaignManager.Core;
using CampaignManager.Core.Catalogs;
using CampaignManager.Core.Characters;
using CampaignManager.Core.Checks;
using CampaignManager.Core.Dice;
using CampaignManager.UI.Checks;
using Xunit;

namespace CampaignManager.UI.Tests;

/// <summary>
/// Диалог проверки: порядок шагов, блокировка условий, Удача, повтор, отметка. Правила — в Core
/// (<c>Core.Tests/Checks</c>); здесь — что панель их зовёт и что лист она сама не пишет.
/// </summary>
public sealed class ChecksTests : KitContext
{
    private static readonly SkillDefinition Spot = new(Guid.NewGuid(), "Внимание")
    {
        Code = "skill.spot-hidden", BaseValue = 25, Category = SkillCategory.InformationGathering,
    };

    private static readonly SkillDefinition Firearms = new(Guid.NewGuid(), "Стрельба") { Code = SkillCodes.Firearms, Category = SkillCategory.CombatFirearms };

    private static readonly SkillDefinition Handgun = new(Guid.NewGuid(), "Стрельба (пистолет)")
    {
        Code = "skill.firearms.handgun", BaseValue = 20, ParentId = Firearms.Id, Category = SkillCategory.CombatFirearms,
    };

    private static readonly SkillCatalog Catalog = new([Spot, Firearms, Handgun]);

    private static CharacterSheet Sheet(int luck = 40) => new()
    {
        Characteristics = new Characteristics { Str = 50, Con = 50, Siz = 50, Dex = 50, App = 50, Int = 50, Pow = 50, Edu = 50 },
        Current = new CurrentValues { Luck = luck },
        Skills = [new SheetSkill { SkillId = Spot.Id, Value = 60 }, new SheetSkill { SkillId = Handgun.Id, Value = 40 }],
    };

    private IRenderedComponent<SkillCheckPanel> Panel(CharacterSheet sheet, string key, List<CheckSheetChange>? changes = null,
        Difficulty difficulty = Difficulty.Regular, bool inCombat = false) =>
        Render<SkillCheckPanel>(p => p
            .Add(c => c.Sheet, sheet)
            .Add(c => c.Catalog, Catalog)
            .Add(c => c.InitialKey, key)
            .Add(c => c.InitialDifficulty, difficulty)
            .Add(c => c.InCombat, inCombat)
            .Add(c => c.OnSheetChange, change => changes?.Add(change)));

    private static void Enter(IRenderedComponent<SkillCheckPanel> cut, string step, int roll) =>
        cut.Find($"[data-testid='check-roll-{step}'] input").Change(roll.ToString(System.Globalization.CultureInfo.InvariantCulture));

    [Fact]
    public void Entered_roll_shows_level_and_locks_conditions()
    {
        var cut = Panel(Sheet(), $"skill:{Spot.Id}");

        Enter(cut, "first", 12);

        Assert.Equal("Чрезвычайный успех", cut.Find("[data-testid='check-level']").TextContent.Trim());
        Assert.True(cut.Find("[data-testid='check-target']").HasAttribute("disabled"));
        Assert.All(cut.FindAll("[data-testid^='check-difficulty-']"), b => Assert.True(b.HasAttribute("disabled")));
    }

    [Fact]
    public void Roll_button_uses_the_dice_and_bonus_die()
    {
        // Единицы 4, десятки 7 и 2 — с бонусной костью берётся меньшее: 24.
        Dice.Enqueue(4, 7, 2);
        var cut = Panel(Sheet(), $"skill:{Spot.Id}");
        cut.Find("[data-testid='check-dice-1']").Click();

        cut.FindAll("[data-testid='check-roll-first'] button").Single(b => b.TextContent.Contains("Бросить")).Click();

        Assert.Equal("24", cut.Find(".check-roll").TextContent);
        Assert.Contains("кости 74, 24", cut.Find("[data-testid='check-result']").TextContent);
    }

    /// <summary>Удача: панель отдаёт трату родителю и сама лист не меняет.</summary>
    [Fact]
    public void Spending_luck_is_reported_not_written()
    {
        var sheet = Sheet(luck: 40);
        List<CheckSheetChange> changes = [];
        var cut = Panel(sheet, $"skill:{Spot.Id}", changes);

        Enter(cut, "first", 70);
        cut.Find("[data-testid='check-luck-Regular']").Click();

        var change = Assert.Single(changes);
        Assert.Equal(10, change.LuckCost);
        Assert.Equal(40, sheet.Current.Luck);
        Assert.NotNull(cut.Find("[data-testid='check-luck-result']"));
        // Удача и повтор не вместе: после траты блока повтора нет
        Assert.Empty(cut.FindAll("[data-testid='check-push']"));
        // успех куплен — отметки нет
        Assert.Contains("куплен Удачей", cut.Find("[data-testid='check-mark']").TextContent);
    }

    [Fact]
    public void Too_expensive_luck_option_is_disabled()
    {
        var cut = Panel(Sheet(luck: 5), $"skill:{Spot.Id}");

        Enter(cut, "first", 70);

        Assert.True(cut.Find("[data-testid='check-luck-Regular']").HasAttribute("disabled"));
    }

    [Fact]
    public void Success_offers_mark_and_reports_it()
    {
        List<CheckSheetChange> changes = [];
        var cut = Panel(Sheet(), $"skill:{Spot.Id}", changes);

        Enter(cut, "first", 40);
        cut.Find("[data-testid='check-mark-skill']").Click();

        Assert.Equal(Spot.Id, Assert.Single(changes).Mark?.SkillId);
        Assert.Contains("отмечен", cut.Find("[data-testid='check-mark']").TextContent);
    }

    /// <summary>
    /// Успех отметили, потом подняли Удачей: купленный успех отметки не даёт (стр. 97) — трата уходит вместе
    /// со снятием своей отметки, и предложить её снова панель уже не может.
    /// </summary>
    [Fact]
    public void Raising_marked_success_by_luck_undoes_mark_of_this_roll()
    {
        var sheet = Sheet(luck: 40);
        List<CheckSheetChange> changes = [];
        var cut = Panel(sheet, $"skill:{Spot.Id}", changes);

        Enter(cut, "first", 40);
        cut.Find("[data-testid='check-mark-skill']").Click();
        Assert.Contains("отметка за этот бросок снимется", cut.Find("[data-testid='check-luck-raise-note']").TextContent);
        cut.Find("[data-testid='check-luck-Hard']").Click();

        Assert.Equal(2, changes.Count);
        Assert.Equal(10, changes[1].LuckCost);
        Assert.Equal(Spot.Id, changes[1].UndoMarkOfThisRoll?.SkillId);
        Assert.Null(changes[1].Mark);
        Assert.Contains("куплен Удачей", cut.Find("[data-testid='check-mark']").TextContent);
        Assert.Empty(cut.FindAll("[data-testid='check-mark-skill']"));
    }

    /// <summary>Отметка на листе стояла до броска — трата Удачи её не снимает.</summary>
    [Fact]
    public void Raising_by_luck_keeps_mark_from_earlier_roll()
    {
        var sheet = Sheet(luck: 40);
        sheet.Skills[0].Checked = true;
        List<CheckSheetChange> changes = [];
        var cut = Panel(sheet, $"skill:{Spot.Id}", changes);

        Enter(cut, "first", 40);
        Assert.Contains("уже стоит", cut.Find("[data-testid='check-mark']").TextContent);
        cut.Find("[data-testid='check-luck-Hard']").Click();

        var change = Assert.Single(changes);
        Assert.Equal(10, change.LuckCost);
        Assert.Null(change.UndoMarkOfThisRoll);

        CheckRules.Apply(sheet, Catalog, change);
        Assert.True(sheet.Skills[0].Checked);
        Assert.Equal(30, sheet.Current.Luck);
    }

    [Fact]
    public void Critical_success_offers_no_luck()
    {
        var cut = Panel(Sheet(), $"skill:{Spot.Id}");

        Enter(cut, "first", 1);

        Assert.Empty(cut.FindAll("[data-testid='check-luck']"));
    }

    [Fact]
    public void Push_after_failure_rolls_second_attempt()
    {
        var cut = Panel(Sheet(), $"skill:{Spot.Id}");

        Enter(cut, "first", 70);
        cut.Find("[data-testid='check-push-start']").Click();
        Enter(cut, "push", 30);

        Assert.NotNull(cut.Find("[data-testid='check-push-passed']"));
        Assert.Empty(cut.FindAll("[data-testid='check-luck']")); // на повтор Удачу не тратят
    }

    [Fact]
    public void Firearms_cannot_be_pushed()
    {
        var cut = Panel(Sheet(), $"skill:{Handgun.Id}");

        Enter(cut, "first", 70);

        Assert.Contains("Ближний бой и Стрельбу повторно не проверяют", cut.Find("[data-testid='check-push']").TextContent);
        Assert.Empty(cut.FindAll("[data-testid='check-push-start']"));
    }

    [Fact]
    public void In_combat_nothing_is_pushed()
    {
        var cut = Panel(Sheet(), "char:STR", inCombat: true);

        Enter(cut, "first", 70);

        Assert.StartsWith("В бою повторных проверок не бывает", cut.Find("[data-testid='check-push'] p").TextContent);
    }

    [Fact]
    public void Success_below_difficulty_is_a_failure_with_explanation()
    {
        var cut = Panel(Sheet(), $"skill:{Spot.Id}", difficulty: Difficulty.Hard);

        Enter(cut, "first", 45);

        Assert.Contains("Для трудной проверки этого мало", cut.Find("[data-testid='check-result']").TextContent);
        Assert.NotNull(cut.Find("[data-testid='check-push']"));
    }

    [Fact]
    public void Reset_unlocks_conditions()
    {
        var cut = Panel(Sheet(), $"skill:{Spot.Id}");
        Enter(cut, "first", 70);

        cut.Find("[data-testid='check-reset']").Click();

        Assert.False(cut.Find("[data-testid='check-target']").HasAttribute("disabled"));
        Assert.NotNull(cut.Find("[data-testid='check-roll-first']"));
    }

    [Fact]
    public void Manual_value_without_sheet_hints_instead_of_buttons()
    {
        var cut = Render<SkillCheckPanel>();
        Assert.NotNull(cut.Find("[data-testid='check-roll-hint']"));

        cut.Find("[data-testid='check-value']").Input("50");
        Enter(cut, "first", 60);

        Assert.Empty(cut.FindAll("[data-testid^='check-luck-']"));
        Assert.Contains("Списывают пункты на листе сыщика", cut.Find("[data-testid='check-luck']").TextContent);
    }

    [Fact]
    public void Party_mode_reads_selected_investigators_sheet()
    {
        var ann = new CheckInvestigator(Guid.NewGuid(), "Энн", Sheet(), "Аня");
        var cut = Render<SkillCheckPanel>(p => p
            .Add(c => c.Party, [ann])
            .Add(c => c.Catalog, Catalog)
            .Add(c => c.InitialKey, $"skill:{Spot.Id}"));

        cut.Find("[data-testid='check-subject']").Change(ann.Id.ToString());

        Assert.Contains("Внимание (60)", cut.Find("[data-testid='check-target'] option[selected]").TextContent);
    }

    [Fact]
    public void Party_mode_preselects_the_initial_investigator_and_reports_changes()
    {
        var ann = new CheckInvestigator(Guid.NewGuid(), "Энн", Sheet(), "Аня");
        var bob = new CheckInvestigator(Guid.NewGuid(), "Боб", Sheet(), null);
        Guid? reported = null;
        var cut = Render<SkillCheckPanel>(p => p
            .Add(c => c.Party, [ann, bob])
            .Add(c => c.Catalog, Catalog)
            .Add(c => c.InitialKey, $"skill:{Spot.Id}")
            .Add(c => c.InitialInvestigatorId, bob.Id)
            .Add(c => c.OnInvestigatorChanged, (Guid? id) => reported = id));

        Assert.Equal(bob.Id.ToString(), cut.Find("[data-testid='check-subject'] option[selected]").GetAttribute("value"));
        Assert.Contains("Внимание (60)", cut.Find("[data-testid='check-target'] option[selected]").TextContent);

        cut.Find("[data-testid='check-subject']").Change(ann.Id.ToString());
        Assert.Equal(ann.Id, reported);
    }

    [Fact]
    public void Unknown_initial_investigator_is_ignored()
    {
        var ann = new CheckInvestigator(Guid.NewGuid(), "Энн", Sheet(), "Аня");
        var cut = Render<SkillCheckPanel>(p => p
            .Add(c => c.Party, [ann])
            .Add(c => c.Catalog, Catalog)
            .Add(c => c.InitialInvestigatorId, Guid.NewGuid()));

        Assert.Equal("", cut.Find("[data-testid='check-subject'] option[selected]").GetAttribute("value"));
    }

    [Fact]
    public void Details_appear_under_the_result_only_after_the_roll()
    {
        var cut = Render<SkillCheckPanel>(p => p
            .Add(c => c.Sheet, Sheet())
            .Add(c => c.Catalog, Catalog)
            .Add(c => c.InitialKey, $"skill:{Spot.Id}")
            .Add(c => c.Details, "<p>Исход из сценария</p>"));

        Assert.Empty(cut.FindAll("[data-testid='check-details']"));
        cut.Find("[data-testid='check-roll-first'] input").Change("70");
        Assert.Contains("Исход из сценария", cut.Find("[data-testid='check-details']").TextContent);
    }

    [Fact]
    public void Modal_passes_investigator_and_details_through()
    {
        var ann = new CheckInvestigator(Guid.NewGuid(), "Энн", Sheet(), "Аня");
        var cut = Render<SkillCheckModal>(p => p
            .Add(m => m.Open, true)
            .Add(m => m.Party, [ann])
            .Add(m => m.Catalog, Catalog)
            .Add(m => m.InitialKey, $"skill:{Spot.Id}")
            .Add(m => m.InitialInvestigatorId, ann.Id)
            .Add(m => m.Details, "<p>Исход</p>"));

        cut.Find("[data-testid='check-roll-first'] input").Change("70");
        Assert.Contains("Исход", cut.Find("[data-testid='check-details']").TextContent);
    }

    [Fact]
    public void Modal_recreates_panel_on_every_open()
    {
        var cut = Render<SkillCheckModal>(p => p
            .Add(m => m.Open, true)
            .Add(m => m.Sheet, Sheet())
            .Add(m => m.Catalog, Catalog)
            .Add(m => m.InitialKey, $"skill:{Spot.Id}"));
        cut.Find("[data-testid='check-roll-first'] input").Change("70");
        Assert.NotNull(cut.Find("[data-testid='check-result']"));

        cut.Render(p => p.Add(m => m.Open, false));
        cut.Render(p => p.Add(m => m.Open, true));

        Assert.Empty(cut.FindAll("[data-testid='check-result']"));
    }
}

/// <summary>Групповая проверка: лучший результат считает Core, панель его подсвечивает.</summary>
public sealed class GroupCheckPanelTests : KitContext
{
    private static readonly SkillDefinition Spot = new(Guid.NewGuid(), "Внимание") { Code = "skill.spot-hidden", BaseValue = 25 };
    private static readonly SkillCatalog Catalog = new([Spot]);

    private static CheckInvestigator Investigator(string name, int spot) =>
        new(Guid.NewGuid(), name, new CharacterSheet { Skills = [new SheetSkill { SkillId = Spot.Id, Value = spot }] });

    [Fact]
    public void Entered_rolls_mark_the_best()
    {
        var ann = Investigator("Энн", 60);
        var bob = Investigator("Боб", 40);
        var cut = Render<GroupCheckPanel>(p => p.Add(c => c.Party, [ann, bob]).Add(c => c.Catalog, Catalog));

        var inputs = cut.FindAll("[data-testid='group-check-row'] input[type='number']");
        inputs[0].Change("50"); // обычный успех
        cut.FindAll("[data-testid='group-check-row'] input[type='number']")[1].Change("8"); // чрезвычайный

        var rows = cut.FindAll("[data-testid='group-check-row']");
        Assert.DoesNotContain("Лучший результат", rows[0].TextContent);
        Assert.Contains("Лучший результат", rows[1].TextContent);
        Assert.Contains("Лучше всех: Боб", cut.Find("[data-testid='group-check-summary']").TextContent);
    }

    [Fact]
    public void Roll_all_rolls_for_everyone()
    {
        Dice.Enqueue(5, 1, 0, 9); // 15 и 95
        var cut = Render<GroupCheckPanel>(p => p
            .Add(c => c.Party, [Investigator("Энн", 60), Investigator("Боб", 40)])
            .Add(c => c.Catalog, Catalog));

        cut.Find("[data-testid='group-check-roll']").Click();

        Assert.Contains("Прошли проверку: 1 из 2", cut.Find("[data-testid='group-check-summary']").TextContent);
    }

    [Fact]
    public void Without_party_investigators_are_entered_by_hand()
    {
        var draft = new GroupCheckDraft();
        var cut = Render<GroupCheckPanel>(p => p.Add(c => c.Draft, draft));
        Assert.NotNull(cut.Find("[data-testid='group-check-empty']"));

        cut.Find("[data-testid='group-check-add']").Click();

        var row = Assert.Single(draft.Manual);
        Assert.Equal(25, row.Value);
        Assert.Single(cut.FindAll("[data-testid='group-check-row']"));
    }
}
