using System.Text;
using System.Text.RegularExpressions;

namespace CampaignManager.Core.Dice;

/// <summary>
/// Запись костей для глаз — <b>единственная точка</b>, где формула превращается в текст (правило 13 дизайн-системы):
/// строчная латинская <c>d</c>, внутри броска пробелов нет, вокруг операторов есть — <c>1d6 + 2</c>, <c>3d6 × 5</c>;
/// не <c>1D6</c>, не <c>1d6+1</c>, не <c>1д6</c>. Хранится формула как её ввели; форматируется только на выводе
/// (<c>DiceText</c> в UI). Разбор для броска — <see cref="DiceFormula"/>, это не он.
/// </summary>
public static partial class DiceNotation
{
    // Кость (1d6, d100, 1D6, 1д6), затем цепочка «оператор + число/кость/бонус к урону». Строка без кости не трогается:
    // «2-3 м» — не бросок. Слева не буква и не цифра (иначе «Ad6» и «21d6» разобрались бы с середины).
    [GeneratedRegex(
        @"(?<![\p{L}\d])(?<first>\d*[dDдД]\d+)(?<rest>(?:\s*[+\-−–×x*]\s*(?:\d*[dDдД]\d+|\d+|Б\.?[кК]\.?[уУ]\.?)(?![\p{L}\d]))*)",
        RegexOptions.CultureInvariant)]
    private static partial Regex Expression();

    [GeneratedRegex(@"\s*(?<op>[+\-−–×x*])\s*(?<term>\d*[dDдД]\d+|\d+|Б\.?[кК]\.?[уУ]\.?)", RegexOptions.CultureInvariant)]
    private static partial Regex Step();

    [GeneratedRegex(@"[dDдД]", RegexOptions.CultureInvariant)]
    private static partial Regex DieLetter();

    /// <summary>Привести все броски в тексте к единому виду; остальной текст остаётся как есть.</summary>
    public static string Format(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text ?? "";
        }

        return Expression().Replace(text, match =>
        {
            var builder = new StringBuilder(Die(match.Groups["first"].Value));
            foreach (Match step in Step().Matches(match.Groups["rest"].Value))
            {
                var op = step.Groups["op"].Value switch
                {
                    "-" or "−" or "–" => "-",
                    "x" or "*" or "×" => "×",
                    _ => "+",
                };
                builder.Append(' ').Append(op).Append(' ').Append(Die(step.Groups["term"].Value));
            }

            return builder.ToString();
        });
    }

    private static string Die(string token) => DieLetter().Replace(token, "d");
}
