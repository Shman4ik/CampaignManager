using CampaignManager.Web.Model;

namespace CampaignManager.Web.Components.Shared.Model;

/// <summary>Порядок записей каталога по одной колонке — по возрастанию или по убыванию.</summary>
public delegate IEnumerable<T> CatalogSort<T>(IEnumerable<T> items, bool ascending);

public static class CatalogSort
{
    /// <summary>Сортировка по ключу обычным сравнением его типа (строки, числа, перечисления).</summary>
    public static CatalogSort<T> By<T, TKey>(Func<T, TKey> key) =>
        (items, ascending) => ascending ? items.OrderBy(key) : items.OrderByDescending(key);
}

/// <summary>
///     Состояние списка на странице каталога (оружие, предметы, заклинания, книги): страница,
///     сортировка по колонке и раскрытая строка. Страница держит у себя только свои фильтры —
///     она отдаёт их сюда функцией <c>filter</c>, а всё остальное одинаково у всех каталогов.
///     <para>
///         Раньше четыре страницы несли четыре копии этих полей и методов, и расхождения между
///         ними были не замыслом, а недоделкой. Новый каталог собирается на этом же классе.
///     </para>
/// </summary>
/// <typeparam name="T">Запись каталога.</typeparam>
public sealed class CatalogListState<T> where T : BaseDataBaseEntity
{
    private readonly Func<IEnumerable<T>> _filter;
    private readonly CatalogSort<T> _defaultSort;
    private readonly IReadOnlyDictionary<string, CatalogSort<T>> _sorts;

    /// <param name="pageSize">Сколько записей на странице.</param>
    /// <param name="filter">Записи, прошедшие фильтры страницы, в любом порядке.</param>
    /// <param name="defaultSort">
    ///     Порядок, пока колонка не выбрана, и для колонки, которой нет в <paramref name="sorts" />.
    /// </param>
    /// <param name="sorts">Порядок по имени колонки — тому, что шлёт <c>SortableTableHeader</c>.</param>
    public CatalogListState(
        int pageSize,
        Func<IEnumerable<T>> filter,
        CatalogSort<T> defaultSort,
        IReadOnlyDictionary<string, CatalogSort<T>>? sorts = null)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(pageSize, 1);

        PageSize = pageSize;
        _filter = filter;
        _defaultSort = defaultSort;
        _sorts = sorts ?? new Dictionary<string, CatalogSort<T>>();
    }

    public int PageSize { get; }

    public int CurrentPage { get; private set; } = 1;

    /// <summary>Колонка, по которой отсортирован список; <c>null</c> — порядок по умолчанию.</summary>
    public string? SortField { get; private set; }

    public bool SortAscending { get; private set; } = true;

    /// <summary>Раскрытая строка списка.</summary>
    public Guid? ExpandedId { get; private set; }

    /// <summary>Сколько записей прошло фильтры.</summary>
    public int TotalItems => _filter().Count();

    /// <summary>Число страниц; пустой список — всё равно одна страница.</summary>
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalItems / (double)PageSize));

    /// <summary>Записи текущей страницы: отфильтрованные, отсортированные и нарезанные.</summary>
    public IReadOnlyList<T> PageItems
    {
        get
        {
            var sort = SortField is not null && _sorts.TryGetValue(SortField, out var byField)
                ? byField
                : _defaultSort;

            return sort(_filter(), SortAscending)
                .Skip((CurrentPage - 1) * PageSize)
                .Take(PageSize)
                .ToList();
        }
    }

    /// <summary>
    ///     Повторное нажатие на ту же колонку меняет направление, новая колонка сортирует по
    ///     возрастанию. Любая смена порядка возвращает на первую страницу.
    /// </summary>
    public void ToggleSort(string field)
    {
        if (SortField == field)
        {
            SortAscending = !SortAscending;
        }
        else
        {
            SortField = field;
            SortAscending = true;
        }

        CurrentPage = 1;
    }

    public void GoToPage(int page)
    {
        if (page >= 1 && page <= TotalPages)
            CurrentPage = page;
    }

    /// <summary>
    ///     Назад на первую страницу — после любой смены фильтра: иначе с десятой страницы
    ///     фильтр уводит в пустой список.
    /// </summary>
    public void ResetPage() => CurrentPage = 1;

    public void ToggleExpanded(T item) => ExpandedId = ExpandedId == item.Id ? null : item.Id;

    public void CollapseAll() => ExpandedId = null;
}
