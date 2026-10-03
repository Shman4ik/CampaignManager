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
/// Навыки листа (решение владельца 2026-10-03, вариант A): сверху «Частые» в заданном порядке, ниже остальные по алфавиту
/// одним списком, каждый блок поровну делится на две колонки (нечётный — лишний слева), дублей между блоками нет.
/// </summary>
public sealed class SkillsPanelOrderTests : KitContext
{
    private static SkillDefinition Skill(string name, string code, int baseValue = 10) =>
        new(Guid.NewGuid(), name) { Code = code, BaseValue = baseValue, Category = SkillCategory.InformationGathering };

    private static readonly SkillCatalog Catalog = new(
    [
        Skill("Слух", "skill.listen"),
        Skill("Внимание", "skill.spot-hidden"),
        Skill("Ёлочные игры", "skill.test-yolka"),
        Skill("Ежевика", "skill.test-ezh"),
        Skill("Яблоки", "skill.test-apple"),
        Skill("Бег", "skill.test-run"),
        Skill("Ухо", "skill.test-ear"),
    ]);

    public SkillsPanelOrderTests() => Services.AddSingleton(Fake.Of<IFilesApi>(new()));

    private IRenderedComponent<CascadingValue<SheetContext>> RenderPanel()
    {
        var context = new SheetContext(new CharacterDto { Sheet = new CharacterSheet(), CanEdit = true }, Catalog, [], null!, () => { }, _ => { });
        return Render<CascadingValue<SheetContext>>(p => p.Add(c => c.Value, context).AddChildContent<SkillsPanel>());
    }

    private static List<string> Names(AngleSharp.Dom.IElement block) =>
        [.. block.QuerySelectorAll(".skill-name-button").Select(b => b.TextContent.Trim())];

    [Fact]
    public void Frequent_block_comes_first_in_owner_order_and_the_rest_is_alphabetical_without_duplicates()
    {
        var cut = RenderPanel();

        var frequent = cut.Find("[data-testid=skills-frequent]");
        var others = cut.Find("[data-testid=skills-others]");

        Assert.Equal(["Внимание", "Слух"], Names(frequent));
        Assert.Equal(["Бег", "Ежевика", "Ёлочные игры", "Ухо", "Яблоки"], Names(others));
        Assert.True(cut.Markup.IndexOf("skills-frequent", StringComparison.Ordinal) < cut.Markup.IndexOf("skills-others", StringComparison.Ordinal));
    }

    [Fact]
    public void Each_block_is_split_in_half_with_the_extra_row_on_the_left()
    {
        var cut = RenderPanel();

        var others = cut.Find("[data-testid=skills-others]");
        Assert.Equal(["Бег", "Ежевика", "Ёлочные игры"], Names(others.QuerySelector("[data-testid=skills-left]")!));
        Assert.Equal(["Ухо", "Яблоки"], Names(others.QuerySelector("[data-testid=skills-right]")!));

        var frequent = cut.Find("[data-testid=skills-frequent]");
        Assert.Equal(["Внимание"], Names(frequent.QuerySelector("[data-testid=skills-left]")!));
        Assert.Equal(["Слух"], Names(frequent.QuerySelector("[data-testid=skills-right]")!));
    }
}
