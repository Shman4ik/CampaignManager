using CampaignManager.Core.Dice;

namespace CampaignManager.Core.Tests.Dice;

/// <summary>
/// Пороги d100 — <see cref="Check.Evaluate"/>, единственная копия на приложение (стр. 87–89).
/// Перенесено из T0.2 (<c>Rules.Tests/Dice/SuccessLevelTests</c>) без правки ожиданий; сложность
/// теперь <see cref="Difficulty"/>, а не уровень успеха, поэтому строк «сложность — критический /
/// провал / крах» нет: такой сложности у проверки не бывает (критический порог нужен только нарастающей
/// очереди, её переносит T2.6).
/// </summary>
[Trait("page", "87-89")]
public sealed class SuccessLevelTests
{
    [Theory]
    // 01 — критический при любом навыке, даже нулевом
    [InlineData(1, 0, SuccessLevel.Critical)]
    [InlineData(1, 1, SuccessLevel.Critical)]
    [InlineData(1, 50, SuccessLevel.Critical)]
    // 100 — крах всегда, даже при навыке 100 и выше
    [InlineData(100, 99, SuccessLevel.Fumble)]
    [InlineData(100, 100, SuccessLevel.Fumble)]
    [InlineData(100, 150, SuccessLevel.Fumble)]
    // 96–99 — крах, только если нужно меньше 50
    [InlineData(96, 49, SuccessLevel.Fumble)]
    [InlineData(99, 49, SuccessLevel.Fumble)]
    [InlineData(95, 49, SuccessLevel.Failure)]
    [InlineData(96, 50, SuccessLevel.Failure)]
    [InlineData(99, 50, SuccessLevel.Failure)]
    [InlineData(96, 99, SuccessLevel.Regular)]
    // Границы /2 и /5 с округлением вниз
    [InlineData(50, 50, SuccessLevel.Regular)]
    [InlineData(51, 50, SuccessLevel.Failure)]
    [InlineData(26, 50, SuccessLevel.Regular)]
    [InlineData(25, 50, SuccessLevel.Hard)]
    [InlineData(11, 50, SuccessLevel.Hard)]
    [InlineData(10, 50, SuccessLevel.Extreme)]
    [InlineData(2, 50, SuccessLevel.Extreme)]
    [InlineData(25, 51, SuccessLevel.Hard)]
    [InlineData(26, 51, SuccessLevel.Regular)]
    [InlineData(10, 51, SuccessLevel.Extreme)]
    [InlineData(24, 49, SuccessLevel.Hard)]
    [InlineData(25, 49, SuccessLevel.Regular)]
    [InlineData(9, 49, SuccessLevel.Extreme)]
    [InlineData(10, 49, SuccessLevel.Hard)]
    // Навык 0–4: чрезвычайного нет (нужен навык ≥ 5), трудный — с навыка 2
    [InlineData(2, 0, SuccessLevel.Failure)]
    [InlineData(2, 1, SuccessLevel.Failure)]
    [InlineData(2, 2, SuccessLevel.Regular)]
    [InlineData(2, 3, SuccessLevel.Regular)]
    [InlineData(2, 4, SuccessLevel.Hard)]
    [InlineData(3, 4, SuccessLevel.Regular)]
    [InlineData(4, 4, SuccessLevel.Regular)]
    [InlineData(5, 4, SuccessLevel.Failure)]
    [InlineData(96, 4, SuccessLevel.Fumble)]
    [InlineData(2, 10, SuccessLevel.Extreme)]
    public void Evaluate_RegularDifficulty_ReturnsLevel(int roll, int skill, SuccessLevel expected)
    {
        Assert.Equal(expected, Check.Evaluate(roll, skill));
        Assert.Equal(expected, Check.Evaluate(roll, skill, Difficulty.Regular));
    }

    [Theory]
    [Trait("page", "88")]
    // Трудная проверка навыка 60: нужно 30 — крах уже на 96 (пример «Работа в библиотеке», стр. 88)
    [InlineData(96, 60, Difficulty.Hard, SuccessLevel.Fumble)]
    [InlineData(96, 55, Difficulty.Hard, SuccessLevel.Fumble)]
    [InlineData(95, 60, Difficulty.Hard, SuccessLevel.Failure)]
    [InlineData(96, 99, Difficulty.Hard, SuccessLevel.Fumble)]
    [InlineData(96, 100, Difficulty.Hard, SuccessLevel.Regular)]
    [InlineData(96, 249, Difficulty.Extreme, SuccessLevel.Fumble)]
    [InlineData(96, 250, Difficulty.Extreme, SuccessLevel.Hard)]
    // Уровень считается от полного навыка, а не от урезанного порога
    [InlineData(30, 60, Difficulty.Hard, SuccessLevel.Hard)]
    [InlineData(31, 60, Difficulty.Hard, SuccessLevel.Regular)]
    [InlineData(10, 60, Difficulty.Hard, SuccessLevel.Extreme)]
    [InlineData(1, 60, Difficulty.Extreme, SuccessLevel.Critical)]
    [InlineData(100, 300, Difficulty.Regular, SuccessLevel.Fumble)]
    public void Evaluate_WithDifficulty_FumbleThresholdFollowsTarget(
        int roll, int skill, Difficulty difficulty, SuccessLevel expected)
    {
        Assert.Equal(expected, Check.Evaluate(roll, skill, difficulty));
    }

    [Theory]
    [Trait("page", "80")]
    [InlineData(60, Difficulty.Regular, 60)]
    [InlineData(60, Difficulty.Hard, 30)]
    [InlineData(61, Difficulty.Hard, 30)]
    [InlineData(60, Difficulty.Extreme, 12)]
    [InlineData(64, Difficulty.Extreme, 12)]
    [InlineData(4, Difficulty.Extreme, 0)]
    public void Target_ReturnsFloorOfDifficultyShare(int skill, Difficulty difficulty, int expected)
    {
        Assert.Equal(expected, Check.Target(skill, difficulty));
    }

    [Theory]
    [InlineData(31, 60, Difficulty.Hard, false)] // обычный успех трудную проверку не проходит
    [InlineData(30, 60, Difficulty.Hard, true)]
    [InlineData(1, 0, Difficulty.Extreme, true)] // 01 — успех любой сложности
    [InlineData(100, 150, Difficulty.Regular, false)] // 100 — крах при любом значении
    [InlineData(12, 60, Difficulty.Extreme, true)]
    [InlineData(13, 60, Difficulty.Extreme, false)]
    public void Succeeds_LevelMustReachDifficulty(int roll, int value, Difficulty difficulty, bool expected)
    {
        Assert.Equal(expected, Check.Succeeds(roll, value, difficulty));
    }

    [Theory]
    [InlineData(SuccessLevel.Critical, "критический успех")]
    [InlineData(SuccessLevel.Extreme, "чрезвычайный успех")]
    [InlineData(SuccessLevel.Hard, "трудный успех")]
    [InlineData(SuccessLevel.Regular, "обычный успех")]
    [InlineData(SuccessLevel.Failure, "провал")]
    [InlineData(SuccessLevel.Fumble, "крах")]
    [InlineData((SuccessLevel)42, "неизвестно")]
    public void RulesText_SuccessLevel_RussianName(SuccessLevel level, string expected)
    {
        Assert.Equal(expected, RulesText.Of(level));
    }

    [Theory]
    [InlineData(Difficulty.Regular, "обычный")]
    [InlineData(Difficulty.Hard, "трудный")]
    [InlineData(Difficulty.Extreme, "чрезвычайный")]
    public void RulesText_Difficulty_RussianName(Difficulty difficulty, string expected)
    {
        Assert.Equal(expected, RulesText.Of(difficulty));
    }
}
