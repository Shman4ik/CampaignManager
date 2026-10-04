namespace CampaignManager.UI.Characters;

/// <summary>
/// Короткие названия навыков для плотного списка режима «Игра» (предложение владельца 2026-10-04): длинные названия
/// («Управление тяжёлыми машинами», «Стрельба (пистолет-пулемёт)») в колонке ~200px шли в две-три строки, и колонки
/// выходили неровными. Полное название не теряется: касание строки открывает окно проверки с ним, диктор читает его из
/// <c>aria-label</c>, мышь — из <c>title</c>. Сокращается только то, что переносилось; остальное — как в справочнике.
/// </summary>
public static class PlaySkillNames
{
    private static readonly Dictionary<string, string> Whole = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Управление тяжёлыми машинами"] = "Тяж. машины",
        ["Обращение с животными"] = "Обращ. с животными",
        ["Вождение автомобиля"] = "Вождение авто",
        ["Стрельба (винтовка/дробовик)"] = "Стрельба (винт./дроб.)",
        ["Стрельба (пистолет-пулемёт)"] = "Стрельба (ПП)",
    };

    /// <summary>Родитель специализации → короткая форма; специализация в скобках остаётся целиком.</summary>
    private static readonly (string Prefix, string Short)[] Parents =
    [
        ("Искусство/ремесло (", "Иск./ремесло ("),
        ("Пилотирование (", "Пилот. ("),
        ("Язык, иностранный (", "Язык ("),
    ];

    /// <summary>Короткое название или то же, если сокращать нечего.</summary>
    public static string Short(string name)
    {
        var trimmed = name.Trim();
        if (Whole.TryGetValue(trimmed, out var whole))
            return whole;

        foreach (var (prefix, shortPrefix) in Parents)
        {
            if (trimmed.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return string.Concat(shortPrefix, trimmed.AsSpan(prefix.Length));
        }

        return trimmed;
    }
}
