namespace CampaignManager.Web.Components.Features.Combat.Services;

/// <summary>
/// Сопоставление названий навыков из разных источников. Каталог оружия хранит навык
/// сокращённо («Стрельба (П)», «Стрельба (В/Д)»), лист сыщика — полностью
/// («Стрельба (пистолет)», «Стрельба (винт./дроб.)»), поэтому сравнение строк «в лоб»
/// давало 0 у любого стрелка.
/// </summary>
public static class SkillNameMatcher
{
    /// <summary>
    /// Сокращения специализаций из каталога оружия → полное название навыка в листе.
    /// Ключи и значения уже нормализованы (нижний регистр, «ё» → «е»).
    /// </summary>
    private static readonly Dictionary<string, string> SpecializationAliases = new(StringComparer.Ordinal)
    {
        ["п"] = "пистолет",
        ["в/д"] = "винтовка/дробовик",
        ["пм"] = "пулемет",
        ["пп"] = "пистолет-пулемет",
        ["ппм"] = "пистолет-пулемет",
        ["тв"] = "тяжелое вооружение",
        ["гаррота"] = "удавка",
        ["копье/винтовка"] = "копье"
    };

    /// <summary>
    /// Специализация по умолчанию, когда в названии оружия её нет: «Ближний бой» без
    /// уточнения — это драка.
    /// </summary>
    private static readonly Dictionary<string, string> DefaultSpecializations = new(StringComparer.Ordinal)
    {
        ["ближний бой"] = "драка"
    };

    private static readonly char[] SpecSeparators = ['/', ',', ' ', '-', '.', ';'];

    /// <summary>Название навыка, разобранное на базу и специализацию.</summary>
    public readonly record struct SkillName(string Base, string? Specialization);

    /// <summary>
    /// Разбирает «Стрельба (П)» на базу «стрельба» и специализацию «пистолет»,
    /// раскрывая сокращения каталога.
    /// </summary>
    public static SkillName Parse(string? name)
    {
        var normalized = Normalize(name);
        if (normalized.Length == 0) return new SkillName(string.Empty, null);

        var open = normalized.IndexOf('(');
        var close = normalized.LastIndexOf(')');
        if (open < 0 || close <= open) return new SkillName(normalized, null);

        var baseName = normalized[..open].Trim();
        var spec = normalized[(open + 1)..close].Trim();
        if (spec.Length == 0) return new SkillName(baseName, null);

        if (SpecializationAliases.TryGetValue(spec, out var expanded)) spec = expanded;
        return new SkillName(baseName, spec);
    }

    /// <summary>Специализация по умолчанию для базового навыка, если она есть.</summary>
    public static string? DefaultSpecializationFor(string baseName) =>
        DefaultSpecializations.GetValueOrDefault(baseName);

    /// <summary>Полные названия совпадают с точностью до регистра, «ё» и пробелов.</summary>
    public static bool FullNameEquals(string? left, string? right) =>
        Normalize(left) == Normalize(right) && Normalize(left).Length > 0;

    /// <summary>
    /// Специализации означают одно и то же: «винт./дроб.» и «винтовка/дробовик» —
    /// одна и та же строка навыка, а «пистолет» и «пистолет-пулемёт» — разные.
    /// </summary>
    public static bool SpecializationMatches(string? left, string? right)
    {
        if (left is null || right is null) return false;
        if (string.Equals(left, right, StringComparison.Ordinal)) return true;

        var leftTokens = Tokenize(left);
        var rightTokens = Tokenize(right);
        if (leftTokens.Length == 0 || leftTokens.Length != rightTokens.Length) return false;

        for (var i = 0; i < leftTokens.Length; i++)
        {
            if (!TokenMatches(leftTokens[i], rightTokens[i])) return false;
        }

        return true;
    }

    /// <summary>
    /// Базовые названия означают один навык, даже если одно длиннее: «Вождение» из правил
    /// погони — это «Вождение автомобиля» в листе, а «Упр. тяж. машинами» — «Управление
    /// тяжёлыми машинами». Сравниваются слова по порядку, лишний хвост допускается.
    /// </summary>
    public static bool BaseMatches(string left, string right)
    {
        if (left.Length == 0 || right.Length == 0) return false;
        if (string.Equals(left, right, StringComparison.Ordinal)) return true;

        var leftTokens = Tokenize(left);
        var rightTokens = Tokenize(right);
        var common = Math.Min(leftTokens.Length, rightTokens.Length);
        if (common == 0) return false;

        for (var i = 0; i < common; i++)
        {
            if (!TokenMatches(leftTokens[i], rightTokens[i])) return false;
        }

        return true;
    }

    /// <summary>
    /// «винт.» и «винтовка» — одно слово: лист сыщика сокращает специализации точкой.
    /// Совпадение по префиксу требует трёх букв, иначе «п» подошло бы и пистолету,
    /// и пулемёту.
    /// </summary>
    private static bool TokenMatches(string left, string right)
    {
        if (string.Equals(left, right, StringComparison.Ordinal)) return true;
        if (Math.Min(left.Length, right.Length) < 3) return false;
        return left.StartsWith(right, StringComparison.Ordinal)
               || right.StartsWith(left, StringComparison.Ordinal);
    }

    private static string[] Tokenize(string specialization) =>
        specialization.Split(SpecSeparators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    /// <summary>Нижний регистр, «ё» → «е», схлопнутые пробелы: разные источники пишут по-разному.</summary>
    private static string Normalize(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return string.Empty;

        var lowered = name.Trim().ToLowerInvariant().Replace('ё', 'е');
        return string.Join(' ', lowered.Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }
}
