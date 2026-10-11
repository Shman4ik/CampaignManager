using Bunit;
using CampaignManager.Contracts.Catalogs;
using CampaignManager.Contracts.Characters;
using CampaignManager.Contracts.Files;
using CampaignManager.Contracts.Identity;
using CampaignManager.Contracts.Music;
using CampaignManager.Contracts.Scenarios;
using CampaignManager.UI.Scenarios;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CampaignManager.UI.Tests;

/// <summary>
/// Иллюстрации локации: в режиме игры — обложка сбоку от названия и галерея во весь экран по касанию; на вкладке «Локации» —
/// миниатюра у названия, галерея справочников в раскрытой строке и окно правки, которое пишет список целиком.
/// </summary>
public sealed class ScenarioLocationImagesTests : KitContext
{
    private static readonly Guid ScenarioId = Guid.Parse("0199b000-0000-7000-8000-000000000500");
    private static readonly Guid CafeId = Guid.Parse("0199b000-0000-7000-8000-000000000501");
    private static readonly Guid PierId = Guid.Parse("0199b000-0000-7000-8000-000000000502");
    private static readonly Guid Cover = Guid.Parse("0199b000-0000-7000-8000-000000000510");
    private static readonly Guid Second = Guid.Parse("0199b000-0000-7000-8000-000000000511");

    private readonly List<LocationImagesInput> _saved = [];

    public ScenarioLocationImagesTests()
    {
        Services.AddSingleton(Fake.Of<IScenariosApi>(new()
        {
            [nameof(IScenariosApi.ListRunsAsync)] = _ => Task.FromResult<IReadOnlyList<ScenarioRunDto>>([]),
            [nameof(IScenariosApi.SetLocationImagesAsync)] = args =>
            {
                _saved.Add((LocationImagesInput)args![2]!);
                return Task.FromResult(new ScenarioLocationDto());
            },
        }));
        Services.AddSingleton(Fake.Of<ICharactersApi>(new()));
        Services.AddSingleton(Fake.Of<IFilesApi>(new()));
        Services.AddSingleton<ICatalogApi<SkillDto>>(new TableFakes.Skills());
        Services.AddSingleton(Fake.Of<ICatalogApi<MusicTrackDto>>(new()));
        AddAuthorization().SetAuthorized("Хранитель").SetPolicies(Policies.Keeper);
    }

    private static ScenarioDto Scenario() => new()
    {
        Id = ScenarioId,
        Name = "Вечер поэзии",
        CanEdit = true,
        Locations =
        [
            new()
            {
                Id = CafeId, Name = "Кофейня",
                Images = [new(Cover, FilesRoutes.Content(Cover), "Вид с пирса"), new(Second, FilesRoutes.Content(Second), null)],
            },
            new() { Id = PierId, Name = "Пирс" },
        ],
    };

    [Fact]
    public void Play_shows_the_cover_as_a_hero_under_the_title_and_opens_the_gallery_full_screen()
    {
        var cut = Render<ScenarioPlay>(p => p.Add(c => c.Scenario, Scenario()).Add(c => c.LocationId, CafeId));

        var hero = cut.Find("[data-testid=location-hero]");
        var cover = cut.Find("[data-testid=location-cover]");
        Assert.Equal("Иллюстрации: Кофейня", cover.GetAttribute("aria-label"));
        Assert.Contains($"{FilesRoutes.Content(Cover)}?w=1200", hero.InnerHtml, StringComparison.Ordinal); // уменьшенная копия, не оригинал
        Assert.Equal("Кофейня", hero.QuerySelector("h2")!.TextContent.Trim()); // название лежит на кадре
        Assert.Equal("2", hero.QuerySelector(".cm-tile-count")!.TextContent.Trim());
        Assert.Empty(cut.FindAll("[data-testid=location-showcase]"));

        cover.Click();
        var showcase = cut.Find("[data-testid=location-showcase]");
        Assert.Contains(FilesRoutes.Content(Cover), showcase.InnerHtml, StringComparison.Ordinal);
        Assert.Equal(2, showcase.QuerySelectorAll("button[aria-label^='Картинка']").Length);
        Assert.Contains("Вид с пирса", showcase.TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public void Play_location_without_images_has_no_cover()
    {
        var cut = Render<ScenarioPlay>(p => p.Add(c => c.Scenario, Scenario()).Add(c => c.LocationId, PierId));

        Assert.Empty(cut.FindAll("[data-testid=location-cover]"));
        Assert.Empty(cut.FindAll("[data-testid=location-hero]"));
        Assert.Equal("Пирс", cut.Find("[data-testid=play-location-detail] h2").TextContent.Trim());
    }

    [Fact]
    public void Locations_tab_shows_thumbnails_gallery_and_saves_the_whole_list()
    {
        var cut = Render<ScenarioLocationsTab>(p => p.Add(t => t.Scenario, Scenario()));

        // Миниатюра у каждого названия (у локации без картинок — заглушка), чтобы названия стояли на одной линии.
        Assert.Equal(2, cut.FindAll("[data-testid=location-row] .cm-thumb").Count);
        Assert.Contains($"{FilesRoutes.Content(Cover)}?w=240", cut.Markup, StringComparison.Ordinal);

        Row(cut, "Кофейня").Click();
        Assert.Single(cut.FindAll("[data-testid=location-gallery]"));
        Assert.Empty(cut.FindAll("[data-testid=add-location-images]")); // у локации с картинками — правка через «⋯»

        Row(cut, "Пирс").Click();
        cut.Find("[data-testid=add-location-images]").Click();
        Assert.Contains("Пирс", cut.Find("[data-testid=location-images]").TextContent, StringComparison.Ordinal);
        cut.FindAll("[data-testid=location-images] button").Single(b => b.TextContent.Trim() == "Отмена").Click();
        Assert.Empty(cut.FindAll("[data-testid=location-images]"));

        cut.Find("[aria-label='Действия: Кофейня']").Click();
        cut.FindAll("[role=menuitem]").Single(i => i.TextContent.Contains("Изменить иллюстрации", StringComparison.Ordinal)).Click();
        cut.FindAll("[data-testid=location-images] button[aria-label='Ниже']")[0].Click(); // обложкой — вторая
        cut.Find("[data-testid=location-images-save]").Click();

        var saved = Assert.Single(_saved);
        Assert.Equal([Second, Cover], saved.Images.Select(i => i.FileId));
        Assert.Equal("Вид с пирса", saved.Images[1].Caption);
    }

    private static AngleSharp.Dom.IElement Row(IRenderedComponent<ScenarioLocationsTab> cut, string name) =>
        cut.FindAll("[data-testid=location-row] button[aria-expanded]").First(b => b.TextContent.Contains(name, StringComparison.Ordinal));
}
