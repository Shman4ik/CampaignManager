using Bunit;
using CampaignManager.Contracts.Characters;
using CampaignManager.Core.Characters;
using CampaignManager.UI.Characters;
using Microsoft.AspNetCore.Components;
using Xunit;

namespace CampaignManager.UI.Tests;

/// <summary>
/// Окно фазы развития: любой бросок можно вписать (правило листа) — d100 проверки опыта, прирост 1d10 и +2d6 Рассудка
/// за 90% уходят в правило Core как есть (пример книги, стр. 92: 85%, выпало 97, +8 — мастерство).
/// </summary>
public sealed class DevelopmentPhaseModalTests : KitContext
{
    [Fact]
    public void Entered_check_gain_and_mastery_sanity_go_to_the_rule()
    {
        var catalog = SkillCatalogs.From([TableFakes.SpotHidden]);
        var sheet = new CharacterSheet
        {
            Current = new CurrentValues { Sanity = 50 },
            Skills = [new SheetSkill { SkillId = TableFakes.SpotHidden.Id, Value = 85, Checked = true }],
        };
        var context = new SheetContext(new CharacterDto { Sheet = sheet, CanEdit = true }, catalog, [TableFakes.SpotHidden], null!, () => { }, _ => { });

        var cut = Render(builder =>
        {
            builder.OpenComponent<CascadingValue<SheetContext>>(0);
            builder.AddAttribute(1, nameof(CascadingValue<SheetContext>.Value), context);
            builder.AddAttribute(2, nameof(CascadingValue<SheetContext>.ChildContent), (RenderFragment)(child =>
            {
                child.OpenComponent<DevelopmentPhaseModal>(0);
                child.AddAttribute(1, nameof(DevelopmentPhaseModal.Open), true);
                child.CloseComponent();
            }));
            builder.CloseComponent();
        });

        var row = cut.Find("[data-testid='dev-skill']");
        row.QuerySelector("input[aria-label='Внимание: выпало']")!.Change("97");
        cut.Find("[data-testid='dev-skill'] input[aria-label='Внимание: прирост: выпало на 1d10']").Change("8");
        cut.Find("[data-testid='dev-skill'] input[aria-label='Внимание: Рассудок за 90%: выпало на 2d6']").Change("11");
        cut.Find("[data-testid='dev-skill-apply']").Click();

        Assert.Equal(93, sheet.Skills.Single().Value);
        Assert.Equal(61, sheet.Current.Sanity);
        Assert.Contains("+8 → 93%", cut.Find("[data-testid='dev-skill']").TextContent);
    }
}
