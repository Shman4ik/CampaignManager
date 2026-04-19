using CampaignManager.Web.Components.Features.Books.Model;
using CampaignManager.Web.Utilities.DataBase;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CampaignManager.Web.Utilities.Api;

public static class BookMigrationApi
{
    public static void MapBookMigrationEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/migration")
            .RequireAuthorization("RequireAdministratorRole")
            .WithTags("Book Migration");

        group.MapPost("/books/import", ImportBooksAsync)
            .WithName("ImportBooks")
            .WithSummary("Импорт книг Мифов / оккультных книг из JSON-тела запроса")
            .WithDescription(
                "Принимает List<Book>. Пропускает книги, у которых Name уже существует. Требует роль Administrator.");
    }

    private static async Task<Results<Ok<BookImportResult>, BadRequest<string>, StatusCodeHttpResult>>
        ImportBooksAsync(
            [FromBody] List<Book> books,
            IDbContextFactory<AppDbContext> dbContextFactory,
            ILogger<Program> logger)
    {
        if (books is null || books.Count == 0)
            return TypedResults.BadRequest("Request body must contain at least one book.");

        try
        {
            await using var db = await dbContextFactory.CreateDbContextAsync();
            var existing = new HashSet<string>(
                await db.Books.Select(b => b.Name).ToListAsync(),
                StringComparer.OrdinalIgnoreCase);

            var skipped = new List<string>();
            var imported = 0;
            var now = DateTimeOffset.UtcNow;

            foreach (var b in books)
            {
                if (string.IsNullOrWhiteSpace(b.Name))
                {
                    skipped.Add("(empty name)");
                    continue;
                }

                if (existing.Contains(b.Name))
                {
                    skipped.Add(b.Name);
                    continue;
                }

                b.Id = Guid.CreateVersion7();
                b.CreatedAt = now;
                b.LastUpdated = now;
                await db.Books.AddAsync(b);
                existing.Add(b.Name);
                imported++;
            }

            await db.SaveChangesAsync();
            logger.LogInformation("Imported {Imported} books, skipped {Skipped}", imported, skipped.Count);

            return TypedResults.Ok(new BookImportResult
            {
                Success = true,
                BooksImported = imported,
                Skipped = skipped,
                Message = $"Imported {imported} books, skipped {skipped.Count}."
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error importing books");
            return TypedResults.StatusCode(500);
        }
    }
}

public class BookImportResult
{
    public bool Success { get; set; }
    public int BooksImported { get; set; }
    public List<string> Skipped { get; set; } = [];
    public string Message { get; set; } = string.Empty;
}
