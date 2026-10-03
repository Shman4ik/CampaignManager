using Bunit;
using CampaignManager.Contracts.Scenarios;
using CampaignManager.UI.Catalogs;
using CampaignManager.UI.Scenarios;
using Bunit.TestDoubles;
using CampaignManager.Contracts.Identity;
using CampaignManager.Contracts.Files;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
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

    // #165: окно «Новый сценарий» открывается адресом — страница обязана перерисоваться при его смене.
    [Fact]
    public void Scenarios_page_opens_and_closes_the_new_scenario_window_on_address_change()
    {
        AddAuthorization().SetAuthorized("Хранитель").SetPolicies(Policies.Keeper);
        Services.AddSingleton(Fake.Of<IScenariosApi>(new()
        {
            [nameof(IScenariosApi.ListAsync)] = _ => Task.FromResult(new ScenarioListDto([], true)),
        }));
        Services.AddSingleton(Fake.Of<IScenarioExchangeApi>(new()));
        var navigation = Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo("scenarios");
        var cut = Render<ScenariosPage>();
        Assert.Empty(cut.FindAll("[data-testid=scenario-form]"));

        navigation.NavigateTo("scenarios/new");
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid=scenario-form]")));

        cut.Find("[data-testid=scenario-form] button.cm-btn-secondary").Click();
        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll("[data-testid=scenario-form]")));
        Assert.EndsWith("/scenarios", navigation.Uri);
    }

    // Аудит U3/U5: ImagePicker Url="_fileUrl" без «@» отдавал превью буквальную строку — картинка всегда битая.
    [Fact]
    public void Handout_form_passes_the_picture_address_value_to_the_preview()
    {
        Services.AddSingleton(Fake.Of<IScenariosApi>(new()));
        Services.AddSingleton(Fake.Of<IFilesApi>(new()));
        var handout = new HandoutDto(Guid.NewGuid(), 1, "Письмо", "Текст", null, Guid.NewGuid(), "/api/v1/files/письмо.png");

        var cut = Render<HandoutFormModal>(p => p
            .Add(m => m.Open, true)
            .Add(m => m.ScenarioId, Guid.NewGuid())
            .Add(m => m.Handout, handout));

        Assert.Equal("/api/v1/files/письмо.png", cut.Find("img.object-cover").GetAttribute("src"));
    }
}
