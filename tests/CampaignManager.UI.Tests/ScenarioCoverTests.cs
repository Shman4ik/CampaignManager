using Bunit;
using Bunit.TestDoubles;
using CampaignManager.Contracts.Files;
using CampaignManager.Contracts.Identity;
using CampaignManager.Contracts.Scenarios;
using CampaignManager.UI.Scenarios;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CampaignManager.UI.Tests;

/// <summary>
/// Обложка сценария: в карточке библиотеки (место под неё — только когда обложка есть хоть у одного сценария) и
/// в форме сценария — поле, которое пишет обложку своим адресом, а шапку не трогает.
/// </summary>
public sealed class ScenarioCoverTests : KitContext
{
    private readonly List<string> _calls = [];

    private static ScenarioSummaryDto Summary(string name, string? cover = null) => new()
    {
        Id = Guid.NewGuid(),
        Name = name,
        IsAuthor = true,
        UpdatedAt = DateTimeOffset.UnixEpoch,
        CanEdit = true,
        CoverUrl = cover,
    };

    private void Api(params ScenarioSummaryDto[] items)
    {
        AddAuthorization().SetAuthorized("Хранитель").SetPolicies(Policies.Keeper);
        Services.AddSingleton(Fake.Of<IScenariosApi>(new()
        {
            [nameof(IScenariosApi.ListAsync)] = _ => Task.FromResult(new ScenarioListDto(items, true)),
            [nameof(IScenariosApi.UpdateAsync)] = _ =>
            {
                _calls.Add("header");
                return Task.FromResult(new ScenarioSavedDto(2, DateTimeOffset.UnixEpoch));
            },
            [nameof(IScenariosApi.SetCoverAsync)] = args =>
            {
                _calls.Add($"cover:{args![1]}");
                return Task.FromResult(new ScenarioSavedDto(2, DateTimeOffset.UnixEpoch));
            },
            [nameof(IScenariosApi.RemoveCoverAsync)] = _ =>
            {
                _calls.Add("remove");
                return Task.CompletedTask;
            },
        }));
        Services.AddSingleton(Fake.Of<IScenarioExchangeApi>(new()));
        Services.AddSingleton(Fake.Of<IFilesApi>(new()));
    }

    [Fact]
    public void Library_shows_covers_and_a_placeholder_only_when_some_scenario_has_one()
    {
        var file = Guid.NewGuid();
        Api(Summary("Башня", $"/api/v1/files/{file}"), Summary("Яма"));
        var cut = Render<ScenariosPage>();
        cut.WaitForState(() => cut.FindAll("[data-testid=scenario-card]").Count == 2);

        var covers = cut.FindAll("[data-testid=scenario-card-cover]");
        Assert.Equal(2, covers.Count);
        Assert.Equal($"/api/v1/files/{file}?w=480", covers[0].QuerySelector("img")!.GetAttribute("src"));
        Assert.Null(covers[1].QuerySelector("img")); // заглушка той же рамки: тексты карточек на одной линии
    }

    [Fact]
    public void Library_without_covers_keeps_cards_as_they_were()
    {
        Api(Summary("Башня"), Summary("Яма"));
        var cut = Render<ScenariosPage>();
        cut.WaitForState(() => cut.FindAll("[data-testid=scenario-card]").Count == 2);

        Assert.Empty(cut.FindAll("[data-testid=scenario-card-cover]"));
        Assert.Contains("md:grid-cols-2", cut.Find("[data-testid=scenario-list]").ClassName, StringComparison.Ordinal);
    }

    [Fact]
    public void Removing_only_the_cover_does_not_write_the_header()
    {
        Api();
        var scenario = new ScenarioDto
        {
            Id = Guid.NewGuid(), Name = "Вечер поэзии", Version = 1, CanEdit = true,
            CoverFileId = Guid.NewGuid(), CoverUrl = "/api/v1/files/abc",
        };
        var saved = false;
        var cut = Render<ScenarioFormModal>(p => p
            .Add(c => c.Open, true)
            .Add(c => c.Scenario, scenario)
            .Add(c => c.OnSaved, () => saved = true));

        Assert.True(cut.Find("[data-testid=scenario-save]").HasAttribute("disabled"));
        cut.FindAll("[data-testid=scenario-cover-field] button").Single(b => b.TextContent.Contains("Убрать", StringComparison.Ordinal)).Click();
        Assert.False(cut.Find("[data-testid=scenario-save]").HasAttribute("disabled"));
        cut.Find("form#scenario-form").Submit();

        cut.WaitForAssertion(() => Assert.True(saved));
        Assert.Equal(["remove"], _calls);
    }

    [Fact]
    public void Header_and_cover_changes_write_the_header_first()
    {
        Api();
        var scenario = new ScenarioDto { Id = Guid.NewGuid(), Name = "Вечер поэзии", Version = 1, CanEdit = true };
        var saved = false;
        var cut = Render<ScenarioFormModal>(p => p
            .Add(c => c.Open, true)
            .Add(c => c.Scenario, scenario)
            .Add(c => c.OnSaved, () => saved = true));

        // Загрузку отдаёт ImagePicker (его покрывают тесты справочников); здесь — его событие.
        var file = new StoredFileDto(Guid.NewGuid(), "/api/v1/files/new", null, "image/webp", 10, "cover.webp", DateTimeOffset.UnixEpoch);
        cut.InvokeAsync(() => cut.FindComponent<CampaignManager.UI.Catalogs.ImagePicker>().Instance.OnChanged.InvokeAsync(file));
        cut.Find("[data-testid=scenario-name]").Input("Вечер поэзии (переделка)");
        cut.Find("form#scenario-form").Submit();

        cut.WaitForAssertion(() => Assert.True(saved));
        Assert.Equal(["header", $"cover:{file.Id}"], _calls);
    }
}
