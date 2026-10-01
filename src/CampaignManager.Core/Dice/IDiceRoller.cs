using System.Security.Cryptography;

namespace CampaignManager.Core.Dice;

/// <summary>
/// Источник случайности для всех бросков приложения. Правила получают его параметром, а не берут
/// статический генератор: так их можно прогнать на заданных костях (тесты, вписанный бросок) и
/// повторить по сиду. В v1 генератор был зашит в <c>CombatService</c>, и детерминированно правила
/// проверялись только через шов в тестах.
/// </summary>
public interface IDiceRoller
{
    /// <summary>Целое из [<paramref name="minInclusive"/>, <paramref name="maxExclusive"/>).</summary>
    int Next(int minInclusive, int maxExclusive);
}

/// <summary>Кости за столом: криптографический генератор, как в v1.</summary>
public sealed class DiceRoller : IDiceRoller
{
    /// <summary>Общий экземпляр: состояния у него нет.</summary>
    public static IDiceRoller Shared { get; } = new DiceRoller();

    private DiceRoller()
    {
    }

    public int Next(int minInclusive, int maxExclusive) =>
        RandomNumberGenerator.GetInt32(minInclusive, maxExclusive);
}

/// <summary>Повторяемые кости: один и тот же сид даёт одни и те же броски (журнал, отладка).</summary>
public sealed class SeededDiceRoller(int seed) : IDiceRoller
{
    private readonly Random _random = new(seed);

    public int Next(int minInclusive, int maxExclusive) => _random.Next(minInclusive, maxExclusive);
}

/// <summary>Короткая запись бросков поверх <see cref="IDiceRoller"/>.</summary>
public static class DiceRollerExtensions
{
    /// <summary>Одна кость: 1..<paramref name="sides"/>.</summary>
    public static int Die(this IDiceRoller roller, int sides) => roller.Next(1, sides + 1);

    /// <summary>Сумма <paramref name="count"/>d<paramref name="sides"/>; ноль костей — ноль, без бросков.</summary>
    public static int Roll(this IDiceRoller roller, int count, int sides)
    {
        var total = 0;
        for (var i = 0; i < count; i++)
            total += roller.Die(sides);
        return total;
    }

    /// <summary>
    /// 1d100; с <paramref name="bonusDice"/> бонусными костями бросают несколько костей десятков и
    /// берут меньший результат (стр. 89). Короткая запись для правил листа.
    /// </summary>
    public static int Percentile(this IDiceRoller roller, int bonusDice = 0) =>
        D100.Roll(roller, bonusDice, 0).Result;
}
