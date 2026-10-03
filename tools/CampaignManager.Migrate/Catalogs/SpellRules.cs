using System.Text.RegularExpressions;

namespace CampaignManager.Migrate.Catalogs;

/// <summary>
/// Заклинания v1 → справочник 2.0 (решения владельца 2026-10-03): один тип вместо составного и одна единица
/// магических очков в стоимости. Правила дословные, без догадок.
/// </summary>
public static partial class SpellRules
{
    /// <summary>
    /// Составной тип «Атака/Проклятие» → тип «Атака», остальное — строка «Также: Проклятие.» в описание. Двойника
    /// «Связь с божеством» тип «Связь» не плодит: он тоже становится «Связь» с пометкой «Также: с божеством».
    /// </summary>
    public static (string Type, string? Also) SplitType(string? type)
    {
        var parts = (type ?? "").Split('/', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            return ("", null);
        }

        var first = parts[0];
        List<string> rest = [.. parts.Skip(1)];
        if (string.Equals(first, "Связь с божеством", StringComparison.OrdinalIgnoreCase))
        {
            first = "Связь";
            rest.Insert(0, "с божеством");
        }

        return (first, rest.Count == 0 ? null : string.Join(", ", rest));
    }

    /// <summary>Строка «Также: …» в конец описания, отдельным абзацем.</summary>
    public static string AppendAlso(string description, string? also)
    {
        if (also is null)
        {
            return description;
        }

        var line = $"Также: {also}.";
        return description.Length == 0 ? line : $"{description.TrimEnd()}\n\n{line}";
    }

    /// <summary>
    /// «10 магии», «1 магия», «Магия варьирует» → «10 ПМ», «1 ПМ», «ПМ варьирует»: одна единица, как в бою; МОЩ — другая
    /// плата и остаётся. Отдельно — «l рассудка» с латинской «l» вместо единицы (опечатка книги).
    /// </summary>
    public static string? Cost(string? cost)
    {
        if (cost is null)
        {
            return null;
        }

        var text = MagicWord().Replace(cost, "ПМ");
        return LatinOne().Replace(text, "1");
    }

    [GeneratedRegex(@"(?<![\p{L}])маги(?:я|и|ю|ей)(?![\p{L}])", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex MagicWord();

    [GeneratedRegex(@"(?<![\p{L}\d])[lIl|](?=\s+рассуд)", RegexOptions.CultureInvariant)]
    private static partial Regex LatinOne();
}
