using CampaignManager.Core;
using Microsoft.AspNetCore.Components;

namespace CampaignManager.UI.Catalogs;

public sealed record CatalogOption(string Value, string Label);

/// <summary>
/// Фильтр-селект справочника: «Все типы» и варианты. Пока он задан, колонку с тем же
/// <see cref="Key"/> страница прячет (<see cref="CatalogView{T}.IsFixed"/>): повторять фильтр в каждой
/// строке незачем — это и были <c>ShowType</c>/<c>ShowEra</c>/<c>ShowMythos</c> v1.
/// </summary>
public sealed record CatalogFilter<T>(string Key, string AllLabel, IReadOnlyList<CatalogOption> Options, Func<T, string, bool> Matches)
{
    /// <summary>Фильтр по enum: варианты — все члены, подписи — <paramref name="label"/>.</summary>
    public static CatalogFilter<T> ForEnum<TEnum>(string key, string allLabel, Func<TEnum, string> label, Func<T, TEnum> value)
        where TEnum : struct, Enum =>
        new(key, allLabel,
            [.. Enum.GetValues<TEnum>().Select(v => new CatalogOption(v.ToString(), label(v)))],
            (item, selected) => value(item).ToString() == selected);
}

/// <summary>
/// Порядок списка для справочника без таблицы (профессии — карточками: заголовков с сортировкой нет). Первый в
/// списке — порядок по умолчанию; выбор — в панели фильтров. Сортировка названием — всегда вторым ключом.
/// </summary>
public sealed record CatalogSort<T>(string Key, string Label, Func<T, object?> By, bool Descending = false);

/// <summary>Флажок-фильтр: «Только редкое», «Только книги Мифов».</summary>
public sealed record CatalogToggle<T>(string Key, string Label, Func<T, bool> Matches);

/// <summary>
/// Что знает разметка справочника о текущем виде: строки страницы, права, какие колонки фиксирует
/// фильтр. Его получают шаблоны колонок, карточек и галереи.
/// </summary>
public sealed class CatalogView<T>
{
    public required IReadOnlyList<T> All { get; init; }

    public required IReadOnlyList<T> Filtered { get; init; }

    /// <summary>Строки текущей страницы (для галереи и карточек; таблица режет страницу сама после сортировки).</summary>
    public required IReadOnlyList<T> Page { get; init; }

    public required bool CanEdit { get; init; }

    /// <summary>Значения фильтров по ключу; нет ключа — «все».</summary>
    public required IReadOnlyDictionary<string, string> Filters { get; init; }

    /// <summary>Выбранная эпоха; null — все.</summary>
    public Era? Era { get; init; }

    /// <summary>Кнопки «Изменить»/«Удалить» записи (пусто без прав).</summary>
    public required RenderFragment<T> Actions { get; init; }

    /// <summary>Открыть форму правки и удалить (с подтверждением) — для своих кнопок карточки.</summary>
    public required Func<T, Task> Edit { get; init; }

    public required Func<T, Task> Delete { get; init; }

    /// <summary>Раскрытая запись (<c>?open=</c>) и переключатель — для галереи.</summary>
    public Guid? Open { get; init; }

    public required Func<T, Task> ToggleOpen { get; init; }

    /// <summary>Фильтр задан — колонку с этим ключом не показывать.</summary>
    public bool IsFixed(string key) => Filters.ContainsKey(key);

    /// <summary>Записи справочника из разных эпох (иначе эпоху не показывать вовсе).</summary>
    public bool ErasVary { get; init; }

    /// <summary>Эпоху в строке показывать, только когда записи из разных эпох и фильтр по эпохе не задан.</summary>
    public bool ShowEra => ErasVary && Era is null;
}
