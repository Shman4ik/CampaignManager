namespace CampaignManager.Web.Components.Features.Combat.Model;

/// <summary>
/// Результат броска d100 с учётом бонусных и штрафных костей (CoC 7e, стр. 89).
/// <para>
/// За каждую бонусную или штрафную кость бросается дополнительная кость десятков.
/// Из всех вариантов выбирается лучший (бонусные) или худший (штрафные) итоговый результат.
/// Одна бонусная кость отменяет одну штрафную.
/// </para>
/// </summary>
public sealed record DiceRollResult
{
    /// <summary>Итоговый результат броска (1–100).</summary>
    public required int Result { get; init; }

    /// <summary>Кость единиц (0–9).</summary>
    public required int Units { get; init; }

    /// <summary>
    /// Все варианты итогового результата — по одному на каждую брошенную кость десятков.
    /// Первый элемент соответствует основной кости.
    /// </summary>
    public required IReadOnlyList<int> Candidates { get; init; }

    /// <summary>Сколько бонусных костей осталось после взаимного погашения.</summary>
    public int BonusDice { get; init; }

    /// <summary>Сколько штрафных костей осталось после взаимного погашения.</summary>
    public int PenaltyDice { get; init; }

    /// <summary>Бросались ли дополнительные кости десятков.</summary>
    public bool HasExtraDice => Candidates.Count > 1;

    /// <summary>Бросок без модификаторов — для ручного ввода и совместимости.</summary>
    public static DiceRollResult Plain(int roll) => new()
    {
        Result = roll,
        Units = roll % 10,
        Candidates = [roll]
    };
}
