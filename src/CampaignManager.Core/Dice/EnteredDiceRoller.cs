namespace CampaignManager.Core.Dice;

/// <summary>
/// Кости с настоящего стола: правило, которое бросает само (<see cref="IDiceRoller"/> параметром — фаза развития,
/// самолечение, Средства), получает вписанные числа, а невписанное бросает <paramref name="fallback"/>. Броски
/// кладутся по порядку, в каком правило их делает; невписанный бросок занимает своё место пустым слотом, поэтому
/// следующий вписанный не уедет в чужую кость.
/// </summary>
public sealed class EnteredDiceRoller(IDiceRoller fallback) : IDiceRoller
{
    private readonly Queue<int?> _slots = new();

    /// <summary>
    /// d100 (1–100): кость единиц и <paramref name="tensDice"/> одинаковых костей десятков — с бонусной или
    /// штрафной костью итог тот же, что вписан. Не вписано или вне 1–100 — бросит генератор.
    /// </summary>
    public EnteredDiceRoller Percentile(int? result, int tensDice = 1)
    {
        if (result is not (>= 1 and <= 100))
        {
            Skip(1 + Math.Max(1, tensDice));
            return this;
        }

        var value = result.Value % 100; // 100 = «00» + «0»
        _slots.Enqueue(value % 10);
        for (var i = 0; i < Math.Max(1, tensDice); i++)
            _slots.Enqueue(value / 10);
        return this;
    }

    /// <summary>
    /// Сумма <paramref name="count"/>d<paramref name="sides"/>, как её вписали («2d6 — выпало 7»): раскладывается по
    /// граням (6 + 1). Не вписано или сумма невозможна для этих костей — бросит генератор.
    /// </summary>
    public EnteredDiceRoller Total(int? total, int count, int sides)
    {
        if (total is not { } sum || count < 1 || sides < 1 || sum < count || sum > count * sides)
        {
            Skip(Math.Max(0, count));
            return this;
        }

        var remaining = sum;
        for (var i = 0; i < count; i++)
        {
            var face = Math.Min(sides, remaining - (count - i - 1));
            _slots.Enqueue(face);
            remaining -= face;
        }

        return this;
    }

    /// <summary>Вписанные грани по одной, в порядке броска.</summary>
    public EnteredDiceRoller Faces(params int[] faces)
    {
        foreach (var face in faces)
            _slots.Enqueue(face);
        return this;
    }

    public int Next(int minInclusive, int maxExclusive) =>
        _slots.TryDequeue(out var slot) && slot is { } face && face >= minInclusive && face < maxExclusive
            ? face
            : fallback.Next(minInclusive, maxExclusive);

    private void Skip(int count)
    {
        for (var i = 0; i < count; i++)
            _slots.Enqueue(null);
    }
}
