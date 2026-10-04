using Bunit;
using CampaignManager.Contracts.Catalogs;
using CampaignManager.UI.Catalogs.Pages;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CampaignManager.UI.Tests;

/// <summary>
/// Картинки профессий — как у бестиария и оружия: обложка (первая картинка) миниатюрой на карточке, у профессии без
/// картинок — заглушка того же места; касание обложки раскрывает карточку с галереей, переключение — по миниатюрам.
/// </summary>
public sealed class OccupationImagesTests : KitContext
{
    private static readonly Guid Cover = Guid.NewGuid();
    private static readonly Guid Second = Guid.NewGuid();

    private readonly OccupationDto _antiquarian = new()
    {
        Id = Guid.NewGuid(),
        Name = "Антиквар",
        CreditRatingMin = 30,
        CreditRatingMax = 70,
        Images = [new CatalogImageDto(Cover, $"/api/v1/files/{Cover}", null), new CatalogImageDto(Second, $"/api/v1/files/{Second}", "В лавке")],
    };

    private readonly OccupationDto _homebrew = new() { Id = Guid.NewGuid(), Name = "Охотник на ведьм", CreditRatingMin = 10, CreditRatingMax = 30 };

    public OccupationImagesTests()
    {
        Services.AddSingleton<ICatalogApi<OccupationDto>>(new FakeCatalog<OccupationDto>(CatalogsRoutes.Occupations, [_antiquarian, _homebrew]));
        Services.AddSingleton<ICatalogApi<SkillDto>>(new FakeCatalog<SkillDto>(CatalogsRoutes.Skills, []));
    }

    [Fact]
    public void Card_shows_cover_thumbnail_and_occupation_without_images_gets_placeholder()
    {
        var page = Render<OccupationsPage>();

        page.WaitForAssertion(() => Assert.Contains("Охотник на ведьм", page.Markup, StringComparison.Ordinal));
        var thumbs = page.FindAll("img[src*='/api/v1/files/']").Select(i => i.GetAttribute("src")).Distinct().ToList();
        Assert.Equal([$"/api/v1/files/{Cover}?w=240"], thumbs);
        Assert.Single(page.FindAll(".oc-cover-empty"));
        Assert.Empty(page.FindAll("figure")); // свёрнутые карточки галерею не рисуют
        // Без картинок раскрывать нечего — у такой профессии название не кнопка.
        Assert.Single(page.FindAll("button[aria-expanded]"), b => b.TextContent.Contains("Антиквар", StringComparison.Ordinal));
        Assert.DoesNotContain(page.FindAll("button[aria-expanded]"), b => b.TextContent.Contains("Охотник", StringComparison.Ordinal));
    }

    [Fact]
    public void Tapping_cover_opens_gallery_and_thumbnails_switch_image()
    {
        var page = Render<OccupationsPage>();
        page.WaitForAssertion(() => Assert.NotNull(page.Find("button.oc-cover")));

        page.Find("button.oc-cover").Click();

        page.WaitForAssertion(() => Assert.NotNull(page.Find("figure > img")));
        Assert.Equal($"/api/v1/files/{Cover}", page.Find("figure > img").GetAttribute("src"));
        Assert.Contains("col-span-full", page.Find($"#occupation-{_antiquarian.Id}").ClassName, StringComparison.Ordinal);
        Assert.Contains($"open={_antiquarian.Id}", Services.GetRequiredService<Microsoft.AspNetCore.Components.NavigationManager>().Uri, StringComparison.Ordinal);

        page.Find("button[aria-label='Картинка 2']").Click();

        Assert.Equal($"/api/v1/files/{Second}", page.Find("figure > img").GetAttribute("src"));
        Assert.Equal("В лавке", page.Find("figcaption").TextContent);
    }

    private sealed class FakeCatalog<T>(CatalogRoute route, IReadOnlyList<T> items) : ICatalogApi<T>
        where T : CatalogItemDto
    {
        public CatalogRoute Route => route;

        public Task<CatalogList<T>> ListAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new CatalogList<T>(items, false));

        public Task<T> CreateAsync(T item, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<T> UpdateAsync(T item, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<CatalogImportReport> ImportAsync(Stream file, bool overwrite, bool dryRun, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<CatalogImportReport> SyncAsync(bool dryRun, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
