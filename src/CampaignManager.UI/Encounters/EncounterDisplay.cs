using System.Text.RegularExpressions;
using CampaignManager.Core.Encounters;

namespace CampaignManager.UI.Encounters;

/// <summary>
/// Тексты правил сцены на экране стола. Ядро (Core) пишет «(стр. 111)» в строках результата и журнала — это справка для
/// разбора, а за столом номера страниц мешают (правило 10: на боевых экранах их не ставят). Журнал в базе хранит текст
/// как есть, чистим при показе.
/// </summary>
public static partial class EncounterDisplay
{
    /// <summary>Убрать «(стр. 110, 114)», «, стр. 113» и «Расстановка (стр. 145):» → «Расстановка:».</summary>
    public static string Plain(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return "";

        var cleaned = WholeParen().Replace(text, "");
        cleaned = TailPage().Replace(cleaned, "");
        return cleaned.Replace(" )", ")").Replace(" .", ".").Replace(" :", ":").Trim();
    }

    /// <summary>Строка — счётчик («Атак за раунд», «Защит за раунд»): внутреннее, не то, что обсуждают за столом.</summary>
    public static bool IsCounter(string label) => label.EndsWith("за раунд", StringComparison.Ordinal) || label == "Проверок очереди";

    /// <summary>
    /// Строка журнала — счётчик боя («Адам: Атак за раунд, 0 → 1»). Новые записи их не несут (<see cref="EffectPreview.IsCounter"/>),
    /// а в журналах, записанных раньше, они остались — при показе их пропускаем (B30).
    /// </summary>
    public static bool IsCounterLine(string line) => CounterLine().IsMatch(line);

    /// <summary>Строки записи журнала для показа: без номеров страниц и без счётчиков.</summary>
    public static IReadOnlyList<string> VisibleLines(EncounterLogEntry entry) =>
        [.. entry.Lines.Where(l => !IsCounterLine(l) && !IsRestatement(entry, l)).Select(Plain).Where(l => l.Length > 0)];

    /// <summary>
    /// Строка движения, которая только повторяет заголовок записи: «Шоггот: локация 5 → 6» уже в тексте, а «Шоггот: Локация, 5 → 6»
    /// и «Шоггот: Тратит 1 действие, 1 → 0» под ним — те же сведения тремя строками (H18). Счётчик потраченных действий — внутреннее.
    /// </summary>
    private static bool IsRestatement(EncounterLogEntry entry, string line)
    {
        if (SpentLine().IsMatch(line))
            return true;

        return LocationLine().Match(line) is { Success: true } move && Plain(entry.Text).Contains($"локация {move.Groups[1].Value} → {move.Groups[2].Value}", StringComparison.Ordinal);
    }

    /// <summary>
    /// Итог записи одной строкой — что изменилось у кого: «Адам Урбан-Фокс: Урон 8, 3 → 0» (первая строка «было → стало» без
    /// пояснения в скобках). Нет такой строки — нет итога (B35).
    /// </summary>
    public static string? Summary(IReadOnlyList<string> lines)
    {
        foreach (var line in lines)
        {
            if (EffectLine().IsMatch(line))
                return ParenTail().Replace(line, "").Trim();
        }

        return null;
    }

    [GeneratedRegex(@"\s*\(стр\.\s*[\d\s,–-]+\)")]
    private static partial Regex WholeParen();

    [GeneratedRegex(@",\s*стр\.\s*[\d\s,–-]+(?=\))")]
    private static partial Regex TailPage();

    [GeneratedRegex(@": Тратит \d+ [^,→]+, \d+ → \d+")]
    private static partial Regex SpentLine();

    [GeneratedRegex(@": Локация, (\d+) → (\d+)")]
    private static partial Regex LocationLine();

    [GeneratedRegex(@": (?:Атак за раунд|Защит за раунд|Проверок очереди), ")]
    private static partial Regex CounterLine();

    // «Кто: Что, было → стало»; в строках броска и урона («Урон: 2d6 = 4, … → 8») есть «=», у итога по участнику его нет.
    [GeneratedRegex(@"^[^:=]+: [^,→=]+, [^,→=]+ → [^→]+$")]
    private static partial Regex EffectLine();

    [GeneratedRegex(@"\s*\([^()]*(?:\([^()]*\)[^()]*)*\)\s*$")]
    private static partial Regex ParenTail();
}
