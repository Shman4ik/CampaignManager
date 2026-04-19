using CampaignManager.Web.Components.Features.Books.Model;
using CampaignManager.Web.Utilities.DataBase;
using CampaignManager.Web.Utilities.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace CampaignManager.Web.Components.Features.Books.Services;

public sealed class BookService(
    IDbContextFactory<AppDbContext> dbContextFactory,
    IMemoryCache cache,
    ILogger<BookService> logger)
{
    private const string BooksKey = "AllBooks";

    public Task<List<Book>> GetAllBooksAsync() =>
        CrudServiceHelper.GetAllCachedAsync<Book>(dbContextFactory, cache, BooksKey, logger);

    public async Task<Book?> GetBookByNameAsync(string name)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        return await dbContext.Books
            .FirstOrDefaultAsync(b => b.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
    }

    public Task<Book?> GetBookByIdAsync(Guid id) =>
        CrudServiceHelper.GetByIdAsync<Book>(dbContextFactory, id, logger);

    public async Task<bool> AddBookAsync(Book book)
    {
        if (string.IsNullOrWhiteSpace(book.Name)) return false;
        return await CrudServiceHelper.CreateAsync(dbContextFactory, cache, BooksKey, book, logger) is not null;
    }

    public Task<bool> UpdateBookAsync(Book book) =>
        CrudServiceHelper.UpdateAsync(dbContextFactory, cache, BooksKey, book, logger);

    public Task<bool> DeleteBookAsync(Guid id) =>
        CrudServiceHelper.DeleteAsync<Book>(dbContextFactory, cache, BooksKey, id, logger);
}
