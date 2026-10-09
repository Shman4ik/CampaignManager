using Bunit;
using CampaignManager.Contracts.Catalogs;
using CampaignManager.UI.Catalogs.Pages;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CampaignManager.UI.Tests;

/// <summary>
/// Предметы — галерея по умолчанию (выбор владельца 2026-10-09): карточки с рисунком группами по типу в алфавитном порядке, без
/// типа — последней группой; касание раскрывает карточку с рисунком целиком; таблица — по переключателю, вид в адресе.
/// </summary>
public sealed class ItemsPageTests : KitContext
{
    private static readonly Guid Picture = Guid.NewGuid();

    private readonly ItemDto _ford = new() { Id = Guid.NewGuid(), Name = "«Форд» модели T", Type = "Транспорт", Price = 360, ImageFileId = Picture, ImageUrl = $"/api/v1/files/{Picture}" };
    private readonly ItemDto _shells = new() { Id = Guid.NewGuid(), Name = "Ружейные патроны", Type = "Боеприпасы", Price = 26, Description = "Цена за 25 штук." };
    private readonly ItemDto _bullets = new() { Id = Guid.NewGuid(), Name = "Патроны .45 ACP", Type = "Боеприпасы", Price = 23 };
    private readonly ItemDto _candle = new() { Id = Guid.NewGuid(), Name = "Свеча" };

    public ItemsPageTests() =>
        Services.AddSingleton<ICatalogApi<ItemDto>>(new FakeItems([_ford, _shells, _bullets, _candle]));

    private NavigationManager Navigation => Services.GetRequiredService<NavigationManager>();

    [Fact]
    public void Gallery_groups_cards_by_type_with_untyped_last()
    {
        var page = Render<ItemsPage>();

        page.WaitForAssertion(() => Assert.Equal(3, page.FindAll("section h2").Count));
        Assert.Equal(["Боеприпасы · 2", "Транспорт · 1", "Без типа · 1"], page.FindAll("section h2").Select(h => h.TextContent.Trim()));
        Assert.Equal($"/api/v1/files/{Picture}?w=480", page.Find(".it-card-media img").GetAttribute("src"));
        // Без цены — прочерк, а не пустая строка; без картинки — заглушка того же места.
        var candle = page.FindAll(".it-card").Single(c => c.TextContent.Contains("Свеча", StringComparison.Ordinal));
        Assert.Contains("—", candle.QuerySelector(".it-card-price")!.TextContent, StringComparison.Ordinal);
        Assert.NotNull(candle.QuerySelector(".it-card-media .fa-box"));
        Assert.Empty(page.FindAll("table"));
    }

    [Fact]
    public void Tapping_card_opens_it_with_full_picture_and_price()
    {
        var page = Render<ItemsPage>();
        page.WaitForAssertion(() => Assert.NotEmpty(page.FindAll(".it-card")));

        page.FindAll(".it-card-button").Single(b => b.TextContent.Contains("Форд", StringComparison.Ordinal)).Click();

        Assert.Contains($"open={_ford.Id}", Navigation.Uri, StringComparison.Ordinal);
        page.WaitForAssertion(() => Assert.Equal($"/api/v1/files/{Picture}", page.Find("img[alt='«Форд» модели T']").GetAttribute("src")));
        Assert.Contains("$360 · Транспорт", page.Find(".it-open").TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public void Price_order_works_inside_groups()
    {
        var page = Render<ItemsPage>();
        page.WaitForAssertion(() => Assert.NotEmpty(page.FindAll(".it-card")));

        page.Find("select[aria-label='Сортировка']").Change("price");

        var ammo = page.FindAll("section")[0].QuerySelectorAll(".it-card-name").Select(n => n.TextContent).ToList();
        Assert.Equal(["Патроны .45 ACP", "Ружейные патроны"], ammo);
    }

    [Fact]
    public void Table_is_one_switch_away_and_lives_in_the_address()
    {
        var page = Render<ItemsPage>();
        page.WaitForAssertion(() => Assert.NotEmpty(page.FindAll(".it-card")));

        page.Find("[data-testid=items-view-table]").Click();

        Assert.Contains("view=table", Navigation.Uri, StringComparison.Ordinal);
        page.WaitForAssertion(() => Assert.NotEmpty(page.FindAll("table th")));
        Assert.Empty(page.FindAll(".it-card"));
        Assert.Equal($"/api/v1/files/{Picture}?w=240", page.Find(".it-thumb img").GetAttribute("src"));

        page.Find("[data-testid=items-view-gallery]").Click();
        Assert.DoesNotContain("view=", Navigation.Uri, StringComparison.Ordinal);
    }

    private sealed class FakeItems(IReadOnlyList<ItemDto> items) : ICatalogApi<ItemDto>
    {
        public CatalogRoute Route => CatalogsRoutes.Items;

        public Task<CatalogList<ItemDto>> ListAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new CatalogList<ItemDto>(items, true));

        public Task<ItemDto> CreateAsync(ItemDto item, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<ItemDto> UpdateAsync(ItemDto item, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<CatalogImportReport> ImportAsync(Stream file, bool overwrite, bool dryRun, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<CatalogImportReport> SyncAsync(bool dryRun, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
