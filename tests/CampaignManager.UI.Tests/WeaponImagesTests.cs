using Bunit;
using CampaignManager.Contracts.Catalogs;
using CampaignManager.UI.Catalogs.Pages;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CampaignManager.UI.Tests;

/// <summary>
/// Картинки оружия — как у бестиария: обложка (первая картинка) миниатюрой в строке, у оружия без картинок — заглушка
/// того же места; раскрытая строка — галерея с переключением по миниатюрам.
/// </summary>
public sealed class WeaponImagesTests : KitContext
{
    private static readonly Guid Cover = Guid.NewGuid();
    private static readonly Guid Second = Guid.NewGuid();

    private readonly WeaponDto _thompson = new()
    {
        Id = Guid.NewGuid(),
        Name = "«Томпсон»",
        Damage = "1d10",
        Images = [new CatalogImageDto(Cover, $"/api/v1/files/{Cover}", null), new CatalogImageDto(Second, $"/api/v1/files/{Second}", "Барабан")],
    };

    public WeaponImagesTests()
    {
        Services.AddSingleton<ICatalogApi<WeaponDto>>(new FakeCatalog<WeaponDto>(CatalogsRoutes.Weapons,
            [_thompson, new WeaponDto { Id = Guid.NewGuid(), Name = "Самопал", Damage = "1d6" }]));
        Services.AddSingleton<ICatalogApi<SkillDto>>(new FakeCatalog<SkillDto>(CatalogsRoutes.Skills, []));
    }

    [Fact]
    public void Row_shows_cover_thumbnail_and_weapon_without_images_gets_placeholder()
    {
        var page = Render<WeaponsPage>();

        page.WaitForAssertion(() => Assert.Contains("Самопал", page.Markup, StringComparison.Ordinal));
        var thumbs = page.FindAll("img[src*='/api/v1/files/']").Select(i => i.GetAttribute("src")).Distinct().ToList();
        Assert.Equal([$"/api/v1/files/{Cover}?w=240"], thumbs);
        Assert.NotEmpty(page.FindAll(".fa-gun"));
    }

    [Fact]
    public void Open_row_shows_gallery_and_switches_image_by_thumbnail()
    {
        var navigation = Services.GetRequiredService<Microsoft.AspNetCore.Components.NavigationManager>();
        navigation.NavigateTo($"weapons?open={_thompson.Id}");

        var page = Render<WeaponsPage>();

        page.WaitForAssertion(() => Assert.NotNull(page.Find("figure img")));
        Assert.Equal($"/api/v1/files/{Cover}", page.Find("figure > img").GetAttribute("src"));

        page.Find("button[aria-label='Картинка 2']").Click();

        Assert.Equal($"/api/v1/files/{Second}", page.Find("figure > img").GetAttribute("src"));
        Assert.Equal("Барабан", page.Find("figcaption").TextContent);
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
