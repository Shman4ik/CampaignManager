namespace CampaignManager.Core.Catalogs;

/// <summary>Строка таблицы кодов: код, имя в справочнике и прочие написания того же имени (v1, старые листы).</summary>
public readonly record struct CatalogCodeRow(string Code, string Name, string[] Aliases)
{
    public CatalogCodeRow(string code, string name)
        : this(code, name, [])
    {
    }
}

/// <summary>
/// Явная таблица «русское имя справочника → код книжной записи» (<c>code</c> в SCHEMA). Код — английское
/// название записи в книгах Chaosium для «Call of Cthulhu» 7e в kebab-case с префиксом справочника
/// (<c>weapon.thompson-submachine-gun</c>); из русского имени он не вычисляется. Поиск — без учёта регистра,
/// «ё» и лишних пробелов, по имени и по написаниям из <see cref="CatalogCodeRow.Aliases"/>. Самодельных
/// записей в таблице нет: их <c>code</c> — null. Только названия и коды, текстов книги здесь нет (D5).
/// Выданный код не меняется: переименование записи — правка имени, а не кода.
/// </summary>
public sealed class CatalogCodeTable
{
    private readonly Dictionary<string, string> codeByName = new(StringComparer.Ordinal);

    public CatalogCodeTable(string prefix, IReadOnlyList<CatalogCodeRow> rows)
    {
        Prefix = prefix;
        var names = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (code, name, aliases) in rows)
        {
            if (!code.StartsWith(prefix, StringComparison.Ordinal))
                throw new InvalidOperationException($"Код «{code}» без префикса «{prefix}».");
            if (!names.TryAdd(code, name))
                throw new InvalidOperationException($"Код «{code}» в таблице дважды.");

            foreach (var spelling in aliases.Prepend(name))
            {
                if (!codeByName.TryAdd(NormalizeName(spelling), code))
                    throw new InvalidOperationException($"Написание «{spelling}» в таблице «{prefix}» дважды.");
            }
        }

        BookNames = names;
    }

    /// <summary>Префикс кодов справочника вместе с точкой: <c>weapon.</c>.</summary>
    public string Prefix { get; }

    /// <summary>Книжные записи: код → имя справочника. Для сидов, переноса и синхронизации с правилами.</summary>
    public IReadOnlyDictionary<string, string> BookNames { get; }

    /// <summary>Код книжной записи по имени или старому написанию; null — записи нет в таблице (самодельная).</summary>
    public string? FromName(string name) => codeByName.GetValueOrDefault(NormalizeName(name));

    /// <summary>Нижний регистр, «ё» → «е», пробелы по краям сняты, подряд идущие схлопнуты.</summary>
    public static string NormalizeName(string name) =>
        string.Join(' ', name.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
            .ToLowerInvariant()
            .Replace('ё', 'е');
}
