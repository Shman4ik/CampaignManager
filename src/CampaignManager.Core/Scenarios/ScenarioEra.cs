using System.Globalization;
using System.Text.RegularExpressions;

namespace CampaignManager.Core.Scenarios;

/// <summary>
/// Эпоха сценария по времени действия, записанному текстом («Июнь 1925 года», «1930-е»): так её узнаёт перенос v1
/// (T1.3) и импорт файла без поля эпохи (T2.5d). Год 1890–1949 — классика; остальное не угадывается.
/// </summary>
public static partial class ScenarioEra
{
    public static Era? FromSettingDate(string? text) =>
        text is not null && YearPattern().Match(text) is { Success: true } m
                         && int.Parse(m.Value, CultureInfo.InvariantCulture) is >= 1890 and < 1950
            ? Era.Classic
            : null;

    [GeneratedRegex(@"1[89]\d\d")]
    private static partial Regex YearPattern();
}
