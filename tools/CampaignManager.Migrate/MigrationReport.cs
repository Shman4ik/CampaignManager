using System.Globalization;
using System.Text;

namespace CampaignManager.Migrate;

/// <summary>
/// Отчёт переноса (<c>docs/v2/migration-report.md</c>): счётчики «было в v1 → стало в cm», всё отброшенное,
/// исправленное и предупреждения. Строки отчёта не содержат почт — только имена и названия.
/// </summary>
public sealed class MigrationReport
{
    private readonly List<CountRow> _counts = [];
    private readonly Dictionary<string, List<string>> _sections = new(StringComparer.Ordinal);
    private readonly List<string> _order = [];

    public sealed record CountRow(string Source, int V1, string Target, int V2, string? Note);

    public IReadOnlyList<CountRow> Counts => _counts;

    public IReadOnlyDictionary<string, List<string>> Sections => _sections;

    public void Count(string source, int v1, string target, int v2, string? note = null) =>
        _counts.Add(new CountRow(source, v1, target, v2, note));

    /// <summary>Строка в раздел отчёта; разделы выводятся в порядке первого упоминания.</summary>
    public void Add(string section, string line)
    {
        if (!_sections.TryGetValue(section, out var lines))
        {
            lines = [];
            _sections[section] = lines;
            _order.Add(section);
        }

        lines.Add(line);
    }

    public int CountOf(string section) => _sections.TryGetValue(section, out var lines) ? lines.Count : 0;

    public string ToMarkdown(DateTimeOffset generatedAt, string? header = null)
    {
        var md = new StringBuilder();
        md.AppendLine("# Перенос данных v1 → v2: отчёт");
        md.AppendLine();
        md.AppendLine(CultureInfo.InvariantCulture,
            $"Сгенерирован `tools/CampaignManager.Migrate` {generatedAt:yyyy-MM-dd HH:mm} UTC. Правила — [SCHEMA.md](SCHEMA.md), «Перенос данных»; решения владельца — [TASKS.md](TASKS.md), T1.3.");
        if (header is not null)
        {
            md.AppendLine();
            md.AppendLine(header);
        }

        md.AppendLine();
        md.AppendLine("## Счётчики");
        md.AppendLine();
        md.AppendLine("| v1 | Строк | cm | Строк | Разница |");
        md.AppendLine("|---|---:|---|---:|---|");
        foreach (var row in _counts)
        {
            md.AppendLine(CultureInfo.InvariantCulture,
                $"| {row.Source} | {row.V1} | {row.Target} | {row.V2} | {Escape(row.Note ?? "")} |");
        }

        foreach (var section in _order)
        {
            md.AppendLine();
            md.AppendLine(CultureInfo.InvariantCulture, $"## {section} ({_sections[section].Count})");
            md.AppendLine();
            foreach (var line in _sections[section])
            {
                md.AppendLine(CultureInfo.InvariantCulture, $"- {line}");
            }
        }

        return md.ToString();
    }

    private static string Escape(string text) => text.Replace("|", "\\|", StringComparison.Ordinal);
}

/// <summary>Названия разделов отчёта — одни и те же у шагов и тестов.</summary>
public static class ReportSections
{
    public const string DroppedModern = "Отброшено: только современная эпоха";
    public const string DroppedDuplicates = "Отброшено: повторы книжных записей";
    public const string DroppedJunk = "Отброшено: мусор и мёртвые данные";
    public const string MovedToScenario = "Перенесено из справочника в сценарий";
    public const string Renamed = "Исправлено: названия (ошибки перевода v1)";
    public const string Fixed = "Исправлено: прочее";
    public const string Homebrew = "Самодельное: без кода";
    public const string SheetSkills = "Листы: навыки без справочника";
    public const string SheetSkillsMapped = "Листы: навыки по старым написаниям";
    public const string Overrides = "Листы: значения книги у НПС (overrides)";
    public const string FormulaWins = "Листы: вычисляемое по формуле, а не как в v1 (решение владельца)";
    public const string SameNames = "Листы: одноимённые (не сливались)";
    public const string Files = "Файлы";
    public const string Warnings = "Предупреждения";
    public const string Checks = "Проверки после переноса";
}
