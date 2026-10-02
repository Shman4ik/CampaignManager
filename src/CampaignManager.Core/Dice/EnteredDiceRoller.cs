namespace CampaignManager.Core.Dice;

/// <summary>
/// Кости с настоящего стола: вписанные числа идут первыми, остальное бросает <paramref name="fallback"/>.
/// Правило листа «любой бросок можно вписать» для правил, которые бросают сами (<see cref="IDiceRoller"/>
/// параметром) — фаза развития, самолечение: окно кладёт сюда выпавшее, и правило считает по нему.
/// </summary>
public sealed class EnteredDiceRoller(IDiceRoller fallback) : IDiceRoller
{
    private readonly Queue<int> _faces = new();

    /// <summary>
    /// Вписанный d100 (1–100): кость единиц и <paramref name="tensDice"/> одинаковых костей десятков — с
    /// бонусной или штрафной костью итог тот же, что вписан. Вне 1–100 — не вписано, бросит генератор.
    /// </summary>
    public EnteredDiceRoller Percentile(int? result, int tensDice = 1)
    {
        if (result is not (>= 1 and <= 100) || tensDice < 1)
            return this;

        var value = result.Value % 100; // 100 = «00» + «0»
        _faces.Enqueue(value % 10);
        for (var i = 0; i < tensDice; i++)
            _faces.Enqueue(value / 10);
        return this;
    }

    /// <summary>Вписанные грани костей (1d10 = одна грань, 2d6 — две) в порядке броска.</summary>
    public EnteredDiceRoller Faces(params int[] faces)
    {
        foreach (var face in faces)
            _faces.Enqueue(face);
        return this;
    }

    public int Next(int minInclusive, int maxExclusive)
    {
        while (_faces.TryDequeue(out var face))
        {
            if (face >= minInclusive && face < maxExclusive)
                return face;
        }

        return fallback.Next(minInclusive, maxExclusive);
    }
}
