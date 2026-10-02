using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization.Metadata;
using CampaignManager.Contracts;
using CampaignManager.Contracts.Catalogs;

namespace CampaignManager.ApiClient.Catalogs;

/// <summary>
/// Клиент справочника. Общий на все семь: различаются адрес и сгенерированные типы JSON. Список
/// кэширует браузер по <c>ETag</c> (сервер отвечает <c>no-cache</c>, браузер перепроверяет и получает
/// 304 без тела).
/// </summary>
public abstract class CatalogApiClient<T>(HttpClient http, CatalogRoute route, JsonTypeInfo<T> itemType, JsonTypeInfo<CatalogList<T>> listType)
    : ICatalogApi<T>
    where T : CatalogItemDto
{
    private static ContractsJsonContext Json => ContractsJsonContext.Default;

    public CatalogRoute Route => route;

    public async Task<CatalogList<T>> ListAsync(CancellationToken cancellationToken = default)
    {
        using var response = await http.GetAsync(route.Base, cancellationToken);
        return await ApiResponses.ReadAsync(response, listType, cancellationToken, withCode: true);
    }

    public async Task<T> CreateAsync(T item, CancellationToken cancellationToken = default)
    {
        using var response = await http.PostAsJsonAsync(route.Base, item, itemType, cancellationToken);
        return await ApiResponses.ReadAsync(response, itemType, cancellationToken, withCode: true);
    }

    public async Task<T> UpdateAsync(T item, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, route.Item(item.Id))
        {
            Content = JsonContent.Create(item, itemType),
        };
        request.Headers.IfMatch.Add(new EntityTagHeaderValue(
            $"\"{item.Version.ToString(CultureInfo.InvariantCulture)}\""));
        using var response = await http.SendAsync(request, cancellationToken);
        return await ApiResponses.ReadAsync(response, itemType, cancellationToken, withCode: true);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using var response = await http.DeleteAsync(route.Item(id), cancellationToken);
        await ApiResponses.EnsureSuccessAsync(response, cancellationToken, withCode: true);
    }

    public async Task<CatalogImportReport> ImportAsync(Stream file, bool overwrite, bool dryRun, CancellationToken cancellationToken = default)
    {
        using var content = new StreamContent(file);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        var url = $"{route.Import}?{CatalogsRoutes.OverwriteQuery}={Flag(overwrite)}&{CatalogsRoutes.DryRunQuery}={Flag(dryRun)}";
        using var response = await http.PostAsync(url, content, cancellationToken);
        return await ApiResponses.ReadAsync(response, Json.CatalogImportReport, cancellationToken, withCode: true);
    }

    public async Task<CatalogImportReport> SyncAsync(bool dryRun, CancellationToken cancellationToken = default)
    {
        using var response = await http.PostAsync($"{route.Sync}?{CatalogsRoutes.DryRunQuery}={Flag(dryRun)}", null, cancellationToken);
        return await ApiResponses.ReadAsync(response, Json.CatalogImportReport, cancellationToken, withCode: true);
    }

    private static string Flag(bool value) => value ? "true" : "false";
}

public sealed class SkillsApiClient(HttpClient http)
    : CatalogApiClient<SkillDto>(http, CatalogsRoutes.Skills, ContractsJsonContext.Default.SkillDto, ContractsJsonContext.Default.CatalogListSkillDto);

public sealed class OccupationsApiClient(HttpClient http)
    : CatalogApiClient<OccupationDto>(http, CatalogsRoutes.Occupations, ContractsJsonContext.Default.OccupationDto, ContractsJsonContext.Default.CatalogListOccupationDto);

public sealed class WeaponsApiClient(HttpClient http)
    : CatalogApiClient<WeaponDto>(http, CatalogsRoutes.Weapons, ContractsJsonContext.Default.WeaponDto, ContractsJsonContext.Default.CatalogListWeaponDto);

public sealed class SpellsApiClient(HttpClient http)
    : CatalogApiClient<SpellDto>(http, CatalogsRoutes.Spells, ContractsJsonContext.Default.SpellDto, ContractsJsonContext.Default.CatalogListSpellDto);

public sealed class BooksApiClient(HttpClient http)
    : CatalogApiClient<BookDto>(http, CatalogsRoutes.Books, ContractsJsonContext.Default.BookDto, ContractsJsonContext.Default.CatalogListBookDto);

public sealed class ItemsApiClient(HttpClient http)
    : CatalogApiClient<ItemDto>(http, CatalogsRoutes.Items, ContractsJsonContext.Default.ItemDto, ContractsJsonContext.Default.CatalogListItemDto);

public sealed class CreaturesApiClient(HttpClient http)
    : CatalogApiClient<CreatureDto>(http, CatalogsRoutes.Creatures, ContractsJsonContext.Default.CreatureDto, ContractsJsonContext.Default.CatalogListCreatureDto);
