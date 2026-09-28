using CampaignManager.Web.Components.Features.Books.Model;
using CampaignManager.Web.Utilities.DataBase;
using CampaignManager.Web.Utilities.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace CampaignManager.Web.Components.Features.Books.Services;

/// <summary>
///     Справочник книг. Читают все вошедшие, правят только Хранитель и администратор —
///     проверка стоит в каждом методе записи, а не только в разметке страницы.
/// </summary>
public sealed class BookService(
    IDbContextFactory<AppDbContext> dbContextFactory,
    IMemoryCache cache,
    IdentityService identityService,
    ILogger<BookService> logger)
{
    private const string BooksKey = "AllBooks";

    public Task<List<Book>> GetAllBooksAsync() =>
        CrudServiceHelper.GetAllCachedAsync<Book>(dbContextFactory, cache, BooksKey, logger);

    public async Task<bool> AddBookAsync(Book book)
    {
        await identityService.EnsureKeeperAsync("добавление книги в справочник");
        if (string.IsNullOrWhiteSpace(book.Name)) return false;
        return await CrudServiceHelper.CreateAsync(dbContextFactory, cache, BooksKey, book, logger) is not null;
    }

    public async Task<bool> UpdateBookAsync(Book book)
    {
        await identityService.EnsureKeeperAsync("изменение книги в справочнике");
        return await CrudServiceHelper.UpdateAsync(dbContextFactory, cache, BooksKey, book, logger);
    }

    public async Task<bool> DeleteBookAsync(Guid id)
    {
        await identityService.EnsureKeeperAsync("удаление книги из справочника");
        return await CrudServiceHelper.DeleteAsync<Book>(dbContextFactory, cache, BooksKey, id, logger);
    }
}
