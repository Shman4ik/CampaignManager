using CampaignManager.Contracts.Catalogs;

namespace CampaignManager.UI.Characters;

/// <summary>
/// Справочники, которые листу нужны только по требованию (поиск оружия, заклинания, книги, твари для
/// привыкания, предмета). Каждый грузится один раз на открытие листа; повторный запрос браузер сверяет по
/// ETag и получает 304.
/// </summary>
public sealed class SheetCatalogs(
    ICatalogApi<WeaponDto> weapons,
    ICatalogApi<SpellDto> spells,
    ICatalogApi<BookDto> books,
    ICatalogApi<CreatureDto> creatures,
    ICatalogApi<ItemDto> items)
{
    private Task<IReadOnlyList<WeaponDto>>? _weapons;
    private Task<IReadOnlyList<SpellDto>>? _spells;
    private Task<IReadOnlyList<BookDto>>? _books;
    private Task<IReadOnlyList<CreatureDto>>? _creatures;
    private Task<IReadOnlyList<ItemDto>>? _items;

    public Task<IReadOnlyList<WeaponDto>> WeaponsAsync() => _weapons ??= Load(weapons);

    public Task<IReadOnlyList<SpellDto>> SpellsAsync() => _spells ??= Load(spells);

    public Task<IReadOnlyList<BookDto>> BooksAsync() => _books ??= Load(books);

    public Task<IReadOnlyList<CreatureDto>> CreaturesAsync() => _creatures ??= Load(creatures);

    public Task<IReadOnlyList<ItemDto>> ItemsAsync() => _items ??= Load(items);

    private static async Task<IReadOnlyList<T>> Load<T>(ICatalogApi<T> api)
        where T : CatalogItemDto
    {
        var list = await api.ListAsync();
        return [.. list.Items.OrderBy(i => i.Name, StringComparer.CurrentCulture)];
    }
}
