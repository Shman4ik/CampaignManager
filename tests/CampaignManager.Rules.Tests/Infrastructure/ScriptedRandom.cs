using CampaignManager.Web.Components.Features.Combat.Services;

namespace CampaignManager.Rules.Tests.Infrastructure;

/// <summary>
///     Подменяемый генератор для веток, где у правил v1 нет «вписать вместо броска».
///     <para>
///         Подставляет заранее заданные числа вместо <c>RandomNumberGenerator</c> через шов
///         <see cref="CombatService.RandomOverride" />. Числа — это то, что вернул бы сам генератор:
///         для <see cref="CombatService.RollDice" /> — грань кости (1..N), для
///         <see cref="CombatService.RollD100(int, int)" /> — сырые кости 0..9 (сначала единицы, потом
///         каждая кость десятков). Через этот же шов бросают и <c>Dice</c> листа, и погоня.
///     </para>
///     <para>
///         Подмена живёт в <see cref="AsyncLocal{T}" />: параллельные тесты её не видят. Выход за
///         пределы запрошенного диапазона или нехватка чисел — ошибка теста, а не тихий бросок.
///     </para>
/// </summary>
public sealed class ScriptedRandom : IDisposable
{
    private readonly Queue<int> _values;

    private ScriptedRandom(IEnumerable<int> values)
    {
        _values = new Queue<int>(values);
        CombatService.RandomOverride.Value = Next;
    }

    /// <summary>Сколько чисел ещё не использовано.</summary>
    public int Remaining => _values.Count;

    /// <summary>Подменить генератор до <see cref="Dispose" />.</summary>
    public static ScriptedRandom Use(params int[] values) => new(values);

    public void Dispose() => CombatService.RandomOverride.Value = null;

    private int Next(int minInclusive, int maxExclusive)
    {
        if (_values.Count == 0)
            throw new InvalidOperationException(
                $"ScriptedRandom: числа кончились (запрошено [{minInclusive}, {maxExclusive})).");

        var value = _values.Dequeue();
        if (value < minInclusive || value >= maxExclusive)
            throw new InvalidOperationException(
                $"ScriptedRandom: {value} вне [{minInclusive}, {maxExclusive}).");
        return value;
    }
}
