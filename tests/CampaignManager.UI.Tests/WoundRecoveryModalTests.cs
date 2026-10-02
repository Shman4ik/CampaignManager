using Bunit;
using CampaignManager.Contracts.Characters;
using CampaignManager.Core;
using CampaignManager.Core.Characters;
using CampaignManager.UI.Characters;
using Microsoft.AspNetCore.Components;
using Xunit;

namespace CampaignManager.UI.Tests;

/// <summary>
/// Лечение вне боя на листе (T2.3b, стр. 119): недельная проверка серьёзной раны с вписанными и брошенными костями,
/// отдых по дням. Окно правит лист на месте и зовёт <see cref="SheetContext.Changed"/> — в базу правку уносит
/// автосохранение листа.
/// </summary>
public sealed class WoundRecoveryModalTests : KitContext
{
    private int _changed;

    [Fact]
    public void Weekly_check_success_heals_extreme_clears_the_wound()
    {
        var sheet = Sheet(hp: 4, majorWound: true);
        var cut = RenderModal(sheet);

        cut.Find("[data-testid='recovery-roll'] input").Change("55"); // ВЫН 60 — обычный успех
        cut.Find("input[aria-label='Лечение раны: выпало на 1d3']").Change("2");
        cut.Find("[data-testid='recovery-week']").Click();

        Assert.Equal(6, sheet.Current.HitPoints);
        Assert.True(sheet.Condition.MajorWound);
        Assert.Contains("+2 ПЗ", cut.Find("[data-testid='recovery-result']").TextContent);

        cut.Find("[data-testid='recovery-roll'] input").Change("10"); // чрезвычайный — до 12
        cut.Find("input[aria-label='Лечение раны: выпало на 2d3']").Change("5");
        cut.Find("[data-testid='recovery-week']").Click();

        Assert.Equal(11, sheet.Current.HitPoints);
        Assert.False(sheet.Condition.MajorWound);
        Assert.Contains("отметка «Серьёзная рана» снята", cut.Find("[data-testid='recovery-result']").TextContent);
        Assert.Equal(2, _changed);
        // Раны больше нет — окно предлагает отдых по дням.
        Assert.NotNull(cut.Find("[data-testid='recovery-days']"));
    }

    [Fact]
    public void Weekly_check_rolled_with_care_and_rest_takes_the_best_of_three_tens()
    {
        var sheet = Sheet(hp: 4, majorWound: true);
        var cut = RenderModal(sheet);

        cut.Find("[data-testid='recovery-care']").Change(true);
        cut.Find("[data-testid='recovery-rest']").Change(true);
        Dice.Enqueue(5, 8, 1, 3); // единицы 5, десятки 8/1/3 → 85, 15, 35; две бонусные — 15
        cut.Find("[data-testid='recovery-roll'] button").Click();
        Dice.Enqueue(3); // 1d3 бросит правило: поле пустое
        cut.Find("[data-testid='recovery-week']").Click();

        Assert.Equal(7, sheet.Current.HitPoints); // 15 против 60 — трудный успех, +1d3 = +3
        Assert.True(sheet.Condition.MajorWound);
        Assert.Contains("ВЫН: 15", cut.Find("[data-testid='recovery-result']").TextContent);
    }

    [Fact]
    public void Failed_weekly_check_changes_nothing_but_is_reported()
    {
        var sheet = Sheet(hp: 4, majorWound: true);
        var cut = RenderModal(sheet);

        cut.Find("[data-testid='recovery-roll'] input").Change("90");
        Assert.Empty(cut.FindAll("input[aria-label^='Лечение раны: выпало на']"));
        cut.Find("[data-testid='recovery-week']").Click();

        Assert.Equal(4, sheet.Current.HitPoints);
        Assert.True(sheet.Condition.MajorWound);
        Assert.Contains("выздоровления нет", cut.Find("[data-testid='recovery-result']").TextContent);
    }

    [Fact]
    public void Rest_without_major_wound_heals_one_per_day_up_to_max()
    {
        var sheet = Sheet(hp: 3, majorWound: false);
        var cut = RenderModal(sheet);

        Assert.Empty(cut.FindAll("[data-testid='recovery-week']"));
        cut.Find("[data-testid='recovery-days']").Change("4");
        Assert.Contains("ПЗ 7 из 12", cut.Find("[data-testid='recovery-preview']").TextContent);
        cut.Find("[data-testid='recovery-rest-apply']").Click();

        Assert.Equal(7, sheet.Current.HitPoints);
        Assert.Equal(1, _changed);

        cut.Find("[data-testid='recovery-days']").Change("30");
        cut.Find("[data-testid='recovery-rest-apply']").Click();
        Assert.Equal(12, sheet.Current.HitPoints);
        Assert.Contains("не ранен", cut.Markup);
    }

    [Fact]
    public void Dying_investigator_is_sent_to_first_aid_and_medicine()
    {
        var sheet = Sheet(hp: 0, majorWound: true);
        sheet.Condition.Dying = true;
        var cut = RenderModal(sheet);

        Assert.Contains("сначала первая помощь", cut.Markup);
        Assert.Empty(cut.FindAll("[data-testid='recovery-week']"));
        Assert.Empty(cut.FindAll("[data-testid='recovery-rest-apply']"));
    }

    private static CharacterSheet Sheet(int hp, bool majorWound)
    {
        var sheet = new CharacterSheet { Current = new CurrentValues { HitPoints = hp } };
        sheet.Characteristics[Characteristic.CON] = 60;
        sheet.Overrides.MaxHitPoints = 12;
        sheet.Condition.MajorWound = majorWound;
        return sheet;
    }

    private IRenderedComponent<CascadingValue<SheetContext>> RenderModal(CharacterSheet sheet)
    {
        var context = new SheetContext(new CharacterDto { Sheet = sheet, CanEdit = true }, SkillCatalogs.From([]), [], null!,
            () => _changed++, _ => { });

        return Render<CascadingValue<SheetContext>>(p => p
            .Add(c => c.Value, context)
            .AddChildContent<WoundRecoveryModal>(m => m.Add(x => x.Open, true)));
    }
}
