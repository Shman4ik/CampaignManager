using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace CampaignManager.Core.Dice;

/// <summary>Кости одного вида в формуле: 2d6, -1d4.</summary>
public sealed record DiceTerm(int Count, int Sides, bool IsNegative = false)
{
    public int Max => Sign * Count * Sides;

    public int Min => Sign * Count;

    private int Sign => IsNegative ? -1 : 1;

    public int Roll(IDiceRoller roller) => Sign * roller.Roll(Count, Sides);

    public override string ToString() => IsNegative ? $"-{Count}d{Sides}" : $"{Count}d{Sides}";
}

/// <summary>
/// Формула костей: «1d6», «2D6+2», «1д8 + 1д6», «-1», «1D6−1». <b>Один</b> разбор на приложение — в v1
/// их было пять, и «1д6», вписанная в помеху или в потерю рассудка, бросалась как 0, а предел
/// привыкания из той же строки считался как 6 (rules-findings F-C01, F-P11).
/// <para>
/// Понимает латинскую и русскую «d»/«д» в любом регистре, типографские минусы, пробелы где угодно,
/// константы и знак перед первой костью. Разбор снисходительный, как в v1: непонятные куски
/// («БкУ», «мусор») пропускаются, а <see cref="IsValid"/> говорит, что они были.
/// </para>
/// </summary>
public sealed partial class DiceFormula
{
    private DiceFormula(string text, IReadOnlyList<DiceTerm> dice, int constant, bool isValid)
    {
        Text = text;
        Dice = dice;
        Constant = constant;
        IsValid = isValid;
    }

    /// <summary>Пустая формула: ноль.</summary>
    public static DiceFormula Zero { get; } = new("0", [], 0, true);

    /// <summary>Исходная строка.</summary>
    public string Text { get; }

    public IReadOnlyList<DiceTerm> Dice { get; }

    /// <summary>Сумма констант.</summary>
    public int Constant { get; }

    /// <summary>Вся строка разобрана; false — в ней были куски, которые бросок пропустил.</summary>
    public bool IsValid { get; }

    /// <summary>Есть ли что бросать.</summary>
    public bool HasDice => Dice.Count > 0;

    /// <summary>
    /// Каждая кость на максимальной грани — урон при чрезвычайном успехе, предел потери рассудка.
    /// У вычитаемой кости это тоже её максимум: «-1d4» даёт −4, как в v1.
    /// </summary>
    public int Max => Dice.Sum(d => d.Max) + Constant;

    /// <summary>Каждая кость на единице.</summary>
    public int Min => Dice.Sum(d => d.Min) + Constant;

    public int Roll(IDiceRoller roller)
    {
        var total = Constant;
        foreach (var term in Dice)
            total += term.Roll(roller);
        return total;
    }

    /// <summary>Разбирает строку. Пустая строка — ноль, а не ошибка.</summary>
    public static DiceFormula Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return new DiceFormula(text ?? string.Empty, [], 0, true);

        var compact = Normalize(text);
        List<DiceTerm> dice = [];
        var constant = 0;
        var valid = compact.Length > 0;

        foreach (var token in Tokenize(compact))
        {
            var match = TermPattern().Match(token);
            if (match.Success)
            {
                var negative = match.Groups["sign"].Value == "-";
                var count = match.Groups["count"].Value is { Length: > 0 } c ? int.Parse(c, CultureInfo.InvariantCulture) : 1;
                dice.Add(new DiceTerm(count, int.Parse(match.Groups["sides"].Value, CultureInfo.InvariantCulture), negative));
            }
            else if (int.TryParse(token, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var flat))
            {
                constant += flat;
            }
            else
            {
                valid = false;
            }
        }

        return new DiceFormula(text, dice, constant, valid);
    }

    /// <summary>Бросок строки: <c>Parse(text).Roll(roller)</c>.</summary>
    public static int Roll(string? text, IDiceRoller roller) => Parse(text).Roll(roller);

    /// <summary>Максимум строки: <c>Parse(text).Max</c>.</summary>
    public static int MaxOf(string? text) => Parse(text).Max;

    public override string ToString() => Text;

    /// <summary>Без пробелов, «д» → «d», типографские минусы → «-», верхний регистр не важен.</summary>
    private static string Normalize(string text)
    {
        var builder = new StringBuilder(text.Length);
        foreach (var ch in text)
        {
            switch (ch)
            {
                case 'd' or 'D' or 'д' or 'Д':
                    builder.Append('d');
                    break;
                case '−' or '–' or '—':
                    builder.Append('-');
                    break;
                default:
                    if (!char.IsWhiteSpace(ch))
                        builder.Append(ch);
                    break;
            }
        }

        return builder.ToString();
    }

    /// <summary>Режет по знакам «+»/«-» (кроме самого первого символа); знак остаётся у куска.</summary>
    private static List<string> Tokenize(string formula)
    {
        List<string> tokens = [];
        var start = 0;

        for (var i = 1; i < formula.Length; i++)
        {
            if (formula[i] is not ('+' or '-'))
                continue;

            if (i > start)
                tokens.Add(formula[start..i]);
            start = i;
        }

        if (start < formula.Length)
            tokens.Add(formula[start..]);

        // «1d6+» — висящий знак ничего не значит, как и в v1.
        tokens.RemoveAll(t => t is "+" or "-");
        return tokens;
    }

    [GeneratedRegex(@"^(?<sign>[+-]?)(?<count>\d*)d(?<sides>\d+)$")]
    private static partial Regex TermPattern();
}
