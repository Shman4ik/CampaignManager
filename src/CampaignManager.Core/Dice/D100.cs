namespace CampaignManager.Core.Dice;

/// <summary>
/// Бросок d100 с подробностями: что выпало на кости единиц и на каждой кости десятков.
/// </summary>
public sealed record D100Roll
{
    /// <summary>Итог (1–100).</summary>
    public required int Result { get; init; }

    /// <summary>Кость единиц (0–9).</summary>
    public required int Units { get; init; }

    /// <summary>Все варианты итога — по одному на кость десятков; первый — основная кость.</summary>
    public required IReadOnlyList<int> Candidates { get; init; }

    /// <summary>Бонусных костей после взаимного погашения.</summary>
    public int BonusDice { get; init; }

    /// <summary>Штрафных костей после взаимного погашения.</summary>
    public int PenaltyDice { get; init; }

    /// <summary>Бросались ли дополнительные кости десятков.</summary>
    public bool HasExtraDice => Candidates.Count > 1;

    /// <summary>
    /// Бросок, вписанный Хранителем с настоящих костей: одна кость десятков, единицы — из числа.
    /// </summary>
    public static D100Roll Entered(int roll) => new()
    {
        Result = roll,
        Units = roll % 10,
        Candidates = [roll],
    };
}

/// <summary>Бросок d100 — один на приложение (стр. 89).</summary>
public static class D100
{
    /// <summary>
    /// Бросок с бонусными и штрафными костями (стр. 89).
    /// <para>
    /// Бросается одна кость единиц и (1 + |нетто|) костей десятков. Из вариантов берётся наименьший
    /// при бонусных костях и наибольший при штрафных. Одна бонусная кость гасит одну штрафную.
    /// «00» + «0» равно 100, поэтому выбор идёт по итогу, а не по кости десятков.
    /// </para>
    /// <para>Генератор отдаёт сырые кости 0..9: сначала единицы, потом каждую кость десятков.</para>
    /// </summary>
    public static D100Roll Roll(IDiceRoller roller, int bonusDice = 0, int penaltyDice = 0)
    {
        var net = Math.Max(0, bonusDice) - Math.Max(0, penaltyDice);
        var units = roller.Next(0, 10);

        var candidates = new int[Math.Abs(net) + 1];
        for (var i = 0; i < candidates.Length; i++)
        {
            var value = roller.Next(0, 10) * 10 + units;
            candidates[i] = value == 0 ? 100 : value;
        }

        var result = net switch
        {
            > 0 => candidates.Min(),
            < 0 => candidates.Max(),
            _ => candidates[0],
        };

        return new D100Roll
        {
            Result = result,
            Units = units,
            Candidates = candidates,
            BonusDice = net > 0 ? net : 0,
            PenaltyDice = net < 0 ? -net : 0,
        };
    }

    /// <summary>
    /// Вписанный бросок, если он есть, иначе бросок костями. Любой бросок можно не бросать, а вписать
    /// с настоящих костей (стр. 117 и правило листа v1).
    /// </summary>
    public static D100Roll RollOrEntered(IDiceRoller roller, int? entered, int bonusDice = 0, int penaltyDice = 0) =>
        entered is { } value ? D100Roll.Entered(value) : Roll(roller, bonusDice, penaltyDice);
}
