using System.Text.RegularExpressions;

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
    public static bool IsCounter(string label) => label.EndsWith("за раунд", StringComparison.Ordinal);

    [GeneratedRegex(@"\s*\(стр\.\s*[\d\s,–-]+\)")]
    private static partial Regex WholeParen();

    [GeneratedRegex(@",\s*стр\.\s*[\d\s,–-]+(?=\))")]
    private static partial Regex TailPage();
}
