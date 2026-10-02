using Bunit;
using CampaignManager.Contracts.Scenarios;
using CampaignManager.UI.Catalogs;
using CampaignManager.UI.Scenarios;
using Xunit;

namespace CampaignManager.UI.Tests;

/// <summary>
/// Сценарии (T2.5a): общий выбор из справочника и дерево локаций. Вёрстку вкладок смотрит браузер, права и запись —
/// интеграционные тесты сервера.
/// </summary>
public sealed class ScenarioTests : KitContext
{
    private sealed record Row(Guid Id, string Name, string Type);

    private static readonly Row[] Rows =
    [
        new(Guid.NewGuid(), "Глубоководный", "Твари Мифов"),
        new(Guid.NewGuid(), "Ёж", "Животные"),
        new(Guid.NewGuid(), "Гуль", "Твари Мифов"),
    ];

    private IRenderedComponent<CatalogPickerModal<Row>> Picker(Action<Row> onPick, string? error = null) =>
        Render<CatalogPickerModal<Row>>(p => p
            .Add(m => m.Open, true)
            .Add(m => m.Title, "Тварь из бестиария")
            .Add(m => m.Load, _ => Task.FromResult<IReadOnlyList<Row>>(Rows))
            .Add(m => m.Key, r => r.Id)
            .Add(m => m.Name, r => r.Name)
            .Add(m => m.Meta, r => r.Type)
            .Add(m => m.Also, r => [r.Type])
            .Add(m => m.Error, error)
            .Add(m => m.OnPick, onPick)
            .Add(m => m.ChildContent, row => builder => builder.AddContent(0, $"Поля для {row.Name}")));

    [Fact]
    public void Picker_searches_without_case_and_yo_and_hands_the_choice_to_the_page()
    {
        Row? picked = null;
        var cut = Picker(r => picked = r);

        Assert.Equal(3, cut.FindAll("[data-testid=picker-row]").Count);
        Assert.True(cut.Find("[data-testid=picker-confirm]").HasAttribute("disabled"));

        cut.Find("[data-testid=picker-search]").Input("ЕЖ");
        var row = Assert.Single(cut.FindAll("[data-testid=picker-row]"));
        row.Click();
        Assert.Contains("Поля для Ёж", cut.Markup);

        cut.Find("[data-testid=picker-confirm]").Click();
        Assert.Equal("Ёж", picked?.Name);
    }

    [Fact]
    public void Picker_finds_by_extra_text_and_keeps_the_server_error_in_the_window()
    {
        var cut = Picker(_ => { }, error: "Такого предмета в справочнике нет.");

        cut.Find("[data-testid=picker-search]").Input("мифов");
        Assert.Equal(2, cut.FindAll("[data-testid=picker-row]").Count);
        Assert.Contains("Такого предмета в справочнике нет.", cut.Find("[data-testid=picker-error]").TextContent);
    }

    [Fact]
    public void Location_tree_gives_depth_siblings_and_path()
    {
        var house = new ScenarioLocationDto { Id = Guid.NewGuid(), Name = "Дом" };
        var cellar = new ScenarioLocationDto { Id = Guid.NewGuid(), Name = "Подвал", ParentId = house.Id };
        var crypt = new ScenarioLocationDto { Id = Guid.NewGuid(), Name = "Склеп", ParentId = cellar.Id };
        var garden = new ScenarioLocationDto { Id = Guid.NewGuid(), Name = "Сад" };
        ScenarioLocationDto[] locations = [house, cellar, crypt, garden];

        Assert.Equal([0, 1, 2, 0], LocationTree.WithDepth(locations).Select(x => x.Depth));
        Assert.Equal([house.Id, garden.Id], LocationTree.Siblings(locations, garden));
        Assert.Equal("Дом › Подвал › Склеп", LocationTree.Path(locations, crypt.Id));
    }

    [Fact]
    public void Workspace_links_keep_play_mode_parameters_for_later()
    {
        var id = Guid.NewGuid();
        var location = Guid.NewGuid();

        Assert.Equal($"scenarios/{id}", ScenarioLinks.Workspace(id, ScenarioLinks.Tabs.Description));
        Assert.Equal($"scenarios/{id}?tab=npcs", ScenarioLinks.Workspace(id, ScenarioLinks.Tabs.Npcs));
        Assert.Equal($"scenarios/{id}?mode=play&location={location}", ScenarioLinks.Play(id, location));
    }
}
