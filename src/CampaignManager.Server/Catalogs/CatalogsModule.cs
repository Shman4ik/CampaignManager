using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using CampaignManager.Contracts;
using CampaignManager.Contracts.Catalogs;
using CampaignManager.Data.Catalogs;
using CampaignManager.Server.Catalogs.Stores;
using CampaignManager.Server.Platform;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Net.Http.Headers;

namespace CampaignManager.Server.Catalogs;

/// <summary>
/// Справочники: навыки, профессии, оружие, заклинания, книги, предметы, бестиарий. Один набор
/// эндпоинтов и один сервис на все (<see cref="CatalogService{TEntity, TDto}"/>), своё у справочника —
/// <see cref="CatalogStore{TEntity, TDto}"/>. Знание модуля — <c>Catalogs/CLAUDE.md</c>.
/// </summary>
public static class CatalogsModule
{
    public static WebApplicationBuilder AddCatalogsModule(this WebApplicationBuilder builder)
    {
        var services = builder.Services;
        services.AddScoped<ISaveChangesInterceptor, CatalogAuditInterceptor>();
        Add<Skill, SkillDto, SkillStore>(services);
        Add<Occupation, OccupationDto, OccupationStore>(services);
        Add<Weapon, WeaponDto, WeaponStore>(services);
        Add<Spell, SpellDto, SpellStore>(services);
        Add<Book, BookDto, BookStore>(services);
        Add<Item, ItemDto, ItemStore>(services);
        Add<Creature, CreatureDto, CreatureStore>(services);
        return builder;
    }

    public static IEndpointRouteBuilder MapCatalogsApi(this IEndpointRouteBuilder app)
    {
        var json = ContractsJsonContext.Default;
        Map<Skill, SkillDto>(app, json.CatalogFileSkillDto);
        Map<Occupation, OccupationDto>(app, json.CatalogFileOccupationDto);
        Map<Weapon, WeaponDto>(app, json.CatalogFileWeaponDto);
        Map<Spell, SpellDto>(app, json.CatalogFileSpellDto);
        Map<Book, BookDto>(app, json.CatalogFileBookDto);
        Map<Item, ItemDto>(app, json.CatalogFileItemDto);
        Map<Creature, CreatureDto>(app, json.CatalogFileCreatureDto);
        return app;
    }

    private static void Add<TEntity, TDto, TStore>(IServiceCollection services)
        where TEntity : CatalogEntry
        where TDto : CatalogItemDto
        where TStore : CatalogStore<TEntity, TDto>, new()
    {
        services.AddSingleton<CatalogStore<TEntity, TDto>>(new TStore());
        services.AddScoped<CatalogService<TEntity, TDto>>();
    }

    private static void Map<TEntity, TDto>(IEndpointRouteBuilder app, JsonTypeInfo<CatalogFile<TDto>> fileType)
        where TEntity : CatalogEntry
        where TDto : CatalogItemDto
    {
        var store = app.ServiceProvider.GetRequiredService<CatalogStore<TEntity, TDto>>();
        var route = store.Route;
        // Тело читается сгенерированным контрактом вручную: параметр-тело обобщённого типа роняет
        // анализатор минимальных API (AD0001).
        var itemType = (JsonTypeInfo<TDto>)ContractsJsonContext.Default.GetTypeInfo(typeof(TDto))!;
        var tag = $"Catalogs: {route.Name}";

        // Читать — любой вошедший; права на запись проверяет сервис (AccessPolicy → 403).
        var group = app.MapGroup(route.Base).RequireAuthorization().WithTags(tag);

        group.MapGet("", async Task<IResult> (HttpContext http, CatalogService<TEntity, TDto> catalog, CancellationToken cancellationToken) =>
        {
            var (list, etag) = await catalog.ListAsync(cancellationToken);
            // no-cache: браузер хранит ответ, но каждый раз сверяет ETag — правка видна сразу,
            // а неизменный справочник приходит 304 без тела.
            http.Response.Headers.CacheControl = "private, no-cache";
            http.Response.Headers.ETag = etag;
            if (http.Request.Headers.IfNoneMatch.Contains(etag))
            {
                return Results.StatusCode(StatusCodes.Status304NotModified);
            }

            return TypedResults.Ok(list);
        }).WithName($"List-{route.Name}");

        group.MapPost("", async Task<IResult> (HttpRequest request, CatalogService<TEntity, TDto> catalog, CancellationToken cancellationToken) =>
                TypedResults.Ok(await catalog.CreateAsync(await BodyAsync(request, itemType, cancellationToken), cancellationToken)))
            .WithName($"Create-{route.Name}");

        group.MapPut("{id:guid}", async Task<IResult> (Guid id, HttpRequest request, CatalogService<TEntity, TDto> catalog, CancellationToken cancellationToken) =>
                TypedResults.Ok(await catalog.UpdateAsync(id, await BodyAsync(request, itemType, cancellationToken), IfMatch(request), cancellationToken)))
            .WithName($"Update-{route.Name}");

        group.MapDelete("{id:guid}", async Task<IResult> (Guid id, CatalogService<TEntity, TDto> catalog, CancellationToken cancellationToken) =>
            {
                await catalog.DeleteAsync(id, cancellationToken);
                return TypedResults.NoContent();
            })
            .WithName($"Delete-{route.Name}");

        group.MapGet("export", async Task<IResult> (CatalogService<TEntity, TDto> catalog, CancellationToken cancellationToken) =>
            {
                var file = await catalog.ExportAsync(cancellationToken);
                var bytes = JsonSerializer.SerializeToUtf8Bytes(file, fileType);
                return TypedResults.File(bytes, "application/json",
                    $"{route.Name}-{DateTime.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}.json");
            })
            .WithName($"Export-{route.Name}");

        // Тело — файл обмена как есть: клиенту не нужно его разбирать.
        group.MapPost("import", async Task<IResult> (HttpRequest request, CatalogService<TEntity, TDto> catalog, CancellationToken cancellationToken) =>
            {
                var file = await BodyAsync(request, fileType, cancellationToken);
                if (file.Items is null)
                {
                    throw ApiProblemException.Invalid("В файле нет списка «items».");
                }

                return TypedResults.Ok(await catalog.ImportAsync(file, Flag(request, CatalogsRoutes.OverwriteQuery),
                    Flag(request, CatalogsRoutes.DryRunQuery), cancellationToken));
            })
            .WithName($"Import-{route.Name}")
            .Accepts<CatalogFile<TDto>>("application/json");

        if (store.HasSeed)
        {
            group.MapPost("sync", async Task<IResult> (HttpRequest request, CatalogService<TEntity, TDto> catalog, CancellationToken cancellationToken) =>
                    TypedResults.Ok(await catalog.SyncAsync(Flag(request, CatalogsRoutes.DryRunQuery), cancellationToken)))
                .WithName($"Sync-{route.Name}");
        }
    }

    private static async Task<T> BodyAsync<T>(HttpRequest request, JsonTypeInfo<T> type, CancellationToken cancellationToken)
    {
        try
        {
            return await JsonSerializer.DeserializeAsync(request.Body, type, cancellationToken)
                ?? throw ApiProblemException.Invalid("Пустое тело запроса.");
        }
        catch (JsonException ex)
        {
            throw ApiProblemException.Invalid($"Не читается как JSON справочника: {ex.Message}");
        }
    }

    private static bool Flag(HttpRequest request, string name) =>
        bool.TryParse(request.Query[name], out var value) && value;

    /// <summary><c>If-Match: "123"</c> → 123; нет заголовка или не версия — null (сервис ответит 428).</summary>
    private static uint? IfMatch(HttpRequest request) =>
        EntityTagHeaderValue.TryParseList(request.Headers.IfMatch, out var tags) && tags.Count == 1
        && uint.TryParse(tags[0].Tag.AsSpan().Trim('"'), NumberStyles.None, CultureInfo.InvariantCulture, out var version)
            ? version
            : null;
}
