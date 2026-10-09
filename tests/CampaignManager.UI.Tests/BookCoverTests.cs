using Bunit;
using CampaignManager.Contracts.Catalogs;
using CampaignManager.Core.Catalogs;
using CampaignManager.UI.Catalogs.Pages;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CampaignManager.UI.Tests;

/// <summary>
/// Обложки книг — как у заклинаний: миниатюра у названия, у книги без обложки — заглушка того же места; раскрытая
/// строка — обложка целиком.
/// </summary>
public sealed class BookCoverTests : KitContext
{
    private static readonly Guid Cover = Guid.NewGuid();

    private readonly BookDto _kingInYellow = new()
    {
        Id = Guid.NewGuid(),
        Name = "Король в жёлтом",
        BookType = BookType.MythosBook,
        ImageFileId = Cover,
        ImageUrl = $"/api/v1/files/{Cover}",
    };

    public BookCoverTests()
    {
        Services.AddSingleton<ICatalogApi<BookDto>>(new FakeCatalog<BookDto>(CatalogsRoutes.Books,
            [_kingInYellow, new BookDto { Id = Guid.NewGuid(), Name = "Золотая ветвь", BookType = BookType.OccultBook }]));
        Services.AddSingleton<ICatalogApi<SpellDto>>(new FakeCatalog<SpellDto>(CatalogsRoutes.Spells, []));
    }

    [Fact]
    public void Row_shows_cover_thumbnail_and_book_without_cover_gets_placeholder()
    {
        var page = Render<BooksPage>();

        page.WaitForAssertion(() => Assert.Contains("Золотая ветвь", page.Markup, StringComparison.Ordinal));
        var thumbs = page.FindAll("img[src*='/api/v1/files/']").Select(i => i.GetAttribute("src")).Distinct().ToList();
        Assert.Equal([$"/api/v1/files/{Cover}?w=240"], thumbs);
        Assert.NotEmpty(page.FindAll("td .fa-book-open, .cm-card .fa-book-open"));
    }

    [Fact]
    public void Open_row_shows_full_cover()
    {
        var navigation = Services.GetRequiredService<Microsoft.AspNetCore.Components.NavigationManager>();
        navigation.NavigateTo($"books?open={_kingInYellow.Id}");

        var page = Render<BooksPage>();

        page.WaitForAssertion(() => Assert.NotNull(page.Find("img[alt='Король в жёлтом']")));
        Assert.Equal($"/api/v1/files/{Cover}", page.Find("img[alt='Король в жёлтом']").GetAttribute("src"));
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
