using Bunit;
using CampaignManager.Contracts.Catalogs;
using CampaignManager.UI.Catalogs.Pages;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CampaignManager.UI.Tests;

/// <summary>
/// Картинки заклинаний — как у оружия: обложка (первая картинка) миниатюрой у названия, у заклинания без картинок —
/// заглушка того же места; раскрытая строка — галерея с переключением по миниатюрам.
/// </summary>
public sealed class SpellImagesTests : KitContext
{
    private static readonly Guid Cover = Guid.NewGuid();
    private static readonly Guid Second = Guid.NewGuid();

    private readonly SpellDto _elderSign = new()
    {
        Id = Guid.NewGuid(),
        Name = "Знак Старших богов",
        SpellType = "Защита",
        Images = [new CatalogImageDto(Cover, $"/api/v1/files/{Cover}", null), new CatalogImageDto(Second, $"/api/v1/files/{Second}", "У врат")],
    };

    public SpellImagesTests()
    {
        Services.AddSingleton<ICatalogApi<SpellDto>>(new FakeCatalog<SpellDto>(CatalogsRoutes.Spells,
            [_elderSign, new SpellDto { Id = Guid.NewGuid(), Name = "Сглаз", SpellType = "Проклятие" }]));
        Services.AddSingleton<ICatalogApi<BookDto>>(new FakeCatalog<BookDto>(CatalogsRoutes.Books, []));
    }

    [Fact]
    public void Row_shows_cover_thumbnail_and_spell_without_images_gets_placeholder()
    {
        var page = Render<SpellsPage>();

        page.WaitForAssertion(() => Assert.Contains("Сглаз", page.Markup, StringComparison.Ordinal));
        var thumbs = page.FindAll("img[src*='/api/v1/files/']").Select(i => i.GetAttribute("src")).Distinct().ToList();
        Assert.Equal([$"/api/v1/files/{Cover}?w=240"], thumbs);
        Assert.NotEmpty(page.FindAll("td .fa-hat-wizard, .cm-card .fa-hat-wizard"));
    }

    [Fact]
    public void Open_row_shows_gallery_and_switches_image_by_thumbnail()
    {
        var navigation = Services.GetRequiredService<Microsoft.AspNetCore.Components.NavigationManager>();
        navigation.NavigateTo($"spells?open={_elderSign.Id}");

        var page = Render<SpellsPage>();

        page.WaitForAssertion(() => Assert.NotNull(page.Find("figure img")));
        Assert.Equal($"/api/v1/files/{Cover}", page.Find("figure > img").GetAttribute("src"));

        page.Find("button[aria-label='Картинка 2']").Click();

        Assert.Equal($"/api/v1/files/{Second}", page.Find("figure > img").GetAttribute("src"));
        Assert.Equal("У врат", page.Find("figcaption").TextContent);
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
