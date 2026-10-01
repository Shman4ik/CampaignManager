using CampaignManager.Core.Dice;

namespace CampaignManager.Core.Catalogs;

/// <summary>
/// Запись потери рассудка «успех/провал» («0/1d6», «1/1д10+2»). Обе части — обычные формулы костей:
/// предел привыкания и сам бросок читают строку одним разбором, поэтому «1д6» больше не даёт предел 6
/// при потере 0 (rules-findings F-P11).
/// </summary>
public sealed record SanityLossFormula(DiceFormula OnSuccess, DiceFormula OnFailure)
{
    /// <summary>Без «/» вся запись — провальная часть, а успех ничего не стоит.</summary>
    public static SanityLossFormula Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return new SanityLossFormula(DiceFormula.Zero, DiceFormula.Zero);

        var slash = text.LastIndexOf('/');
        return slash < 0
            ? new SanityLossFormula(DiceFormula.Zero, DiceFormula.Parse(text))
            : new SanityLossFormula(DiceFormula.Parse(text[..slash]), DiceFormula.Parse(text[(slash + 1)..]));
    }

    /// <summary>
    /// Предел привыкания к ужасному (стр. 167): максимум провальной части, не меньше нуля. Ноль — запись
    /// пустая или не разбирается, предел впишет Хранитель.
    /// </summary>
    public static int MaxLoss(string? text) => Math.Max(0, Parse(text).OnFailure.Max);
}
