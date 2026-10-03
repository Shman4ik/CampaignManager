using Bunit;
using CampaignManager.Contracts.Characters;
using CampaignManager.Contracts.Files;
using CampaignManager.Core.Catalogs;
using CampaignManager.Core.Characters;
using CampaignManager.UI.Characters;
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
        Code = "skill.swim", BaseValue = 20, Category = SkillCategory.Actions,
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
    public void Read_only_sheet_keeps_the_numbers_but_disables_the_controls()
    {
        var cut = Render<PlayVitals>(Sheet(), canEdit: false);

        Assert.True(cut.Find("fieldset.play-fieldset").HasAttribute("disabled"));
    }

    [Fact]
    public void Skills_list_all_with_base_ones_muted_and_search_narrows_the_list()
    {
        var sheet = Sheet();
        sheet.Skills.Add(new SheetSkill { SkillId = Spot.Id, Value = 60, Checked = true });
        sheet.Skills.Add(new SheetSkill { SkillId = Listen.Id, Value = 20 }); // строка есть, но на базе
        var cut = Render<PlaySkills>(sheet);

        // Все три по алфавиту; Плавание без строки листа — на базе справочника
        Assert.Equal(["Внимание", "Плавание", "Слух"], cut.FindAll(".play-skill-name").Select(e => e.TextContent));
        Assert.Equal(["Плавание", "Слух"], cut.FindAll(".play-skill-base .play-skill-name").Select(e => e.TextContent));
        Assert.Single(cut.FindAll(".play-skill-tick i")); // отметка развития видна

        cut.Find("[data-testid='play-skill-search']").Input("плав");
        var swim = Assert.Single(cut.FindAll(".play-skill"));
        Assert.Contains("play-skill-base", swim.ClassName);

        swim.Click();
        Assert.Equal($"skill:{Swim.Id}", Assert.Single(_checks));
    }
}
