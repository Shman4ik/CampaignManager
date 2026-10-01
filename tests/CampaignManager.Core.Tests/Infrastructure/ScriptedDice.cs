using CampaignManager.Core.Dice;

namespace CampaignManager.Core.Tests.Infrastructure;

/// <summary>
/// Кости, которые выпадут, заданы заранее. Числа — то, что вернул бы генератор: для
/// <see cref="DiceRollerExtensions.Die"/> — грань (1..N), для <see cref="D100.Roll"/> — сырые кости
/// 0..9 (сначала единицы, потом каждая кость десятков). Тот же порядок, что у <c>ScriptedRandom</c>
/// тестов T0.2, поэтому числа в перенесённых тестах не менялись.
/// <para>Выход за запрошенный диапазон или нехватка чисел — ошибка теста, а не тихий бросок.</para>
/// </summary>
public sealed class ScriptedDice(params int[] values) : IDiceRoller
{
    private readonly Queue<int> _values = new(values);

    /// <summary>Сколько чисел ещё не использовано.</summary>
    public int Remaining => _values.Count;

    public static ScriptedDice Of(params int[] values) => new(values);

    public int Next(int minInclusive, int maxExclusive)
    {
        if (_values.Count == 0)
            throw new InvalidOperationException(
                $"ScriptedDice: числа кончились (запрошено [{minInclusive}, {maxExclusive})).");

        var value = _values.Dequeue();
        if (value < minInclusive || value >= maxExclusive)
            throw new InvalidOperationException($"ScriptedDice: {value} вне [{minInclusive}, {maxExclusive}).");
        return value;
    }
}
