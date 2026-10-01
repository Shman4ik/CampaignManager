using CampaignManager.Web.Components.Features.Combat.Model;
using CampaignManager.Web.Components.Features.Combat.Services;

namespace CampaignManager.Rules.Tests.Dice;

/// <summary>
///     Пороги d100 — <see cref="CombatService.CalculateSuccessLevel(int, int, SuccessLevel)" />,
///     единственная копия на приложение (стр. 87–89).
/// </summary>
[Trait("page", "87-89")]
public sealed class SuccessLevelTests
{
    [Theory]
    // 01 — критический при любом навыке, даже нулевом
    [InlineData(1, 0, SuccessLevel.CriticalSuccess)]
    [InlineData(1, 1, SuccessLevel.CriticalSuccess)]
    [InlineData(1, 50, SuccessLevel.CriticalSuccess)]
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
    [InlineData(96, 99, SuccessLevel.RegularSuccess)]
    // Границы /2 и /5 с округлением вниз
    [InlineData(50, 50, SuccessLevel.RegularSuccess)]
    [InlineData(51, 50, SuccessLevel.Failure)]
    [InlineData(26, 50, SuccessLevel.RegularSuccess)]
    [InlineData(25, 50, SuccessLevel.HardSuccess)]
    [InlineData(11, 50, SuccessLevel.HardSuccess)]
    [InlineData(10, 50, SuccessLevel.ExtremeSuccess)]
    [InlineData(2, 50, SuccessLevel.ExtremeSuccess)]
    [InlineData(25, 51, SuccessLevel.HardSuccess)]
    [InlineData(26, 51, SuccessLevel.RegularSuccess)]
    [InlineData(10, 51, SuccessLevel.ExtremeSuccess)]
    [InlineData(24, 49, SuccessLevel.HardSuccess)]
    [InlineData(25, 49, SuccessLevel.RegularSuccess)]
    [InlineData(9, 49, SuccessLevel.ExtremeSuccess)]
    [InlineData(10, 49, SuccessLevel.HardSuccess)]
    // Навык 0–4: чрезвычайного нет (нужен навык ≥ 5), трудный — с навыка 2
    [InlineData(2, 0, SuccessLevel.Failure)]
    [InlineData(2, 1, SuccessLevel.Failure)]
    [InlineData(2, 2, SuccessLevel.RegularSuccess)]
    [InlineData(2, 3, SuccessLevel.RegularSuccess)]
    [InlineData(2, 4, SuccessLevel.HardSuccess)]
    [InlineData(3, 4, SuccessLevel.RegularSuccess)]
    [InlineData(4, 4, SuccessLevel.RegularSuccess)]
    [InlineData(5, 4, SuccessLevel.Failure)]
    [InlineData(96, 4, SuccessLevel.Fumble)]
    [InlineData(2, 10, SuccessLevel.ExtremeSuccess)]
    public void CalculateSuccessLevel_RegularDifficulty_ReturnsLevel(int roll, int skill, SuccessLevel expected)
    {
        Assert.Equal(expected, CombatService.CalculateSuccessLevel(roll, skill));
        // Двухаргументная перегрузка — это обычная сложность
        Assert.Equal(expected, CombatService.CalculateSuccessLevel(roll, skill, SuccessLevel.RegularSuccess));
    }

    [Theory]
    [Trait("page", "88")]
    // Трудная проверка навыка 60: нужно 30 — крах уже на 96 (пример «Работа в библиотеке», стр. 88)
    [InlineData(96, 60, SuccessLevel.HardSuccess, SuccessLevel.Fumble)]
    [InlineData(96, 55, SuccessLevel.HardSuccess, SuccessLevel.Fumble)]
    [InlineData(95, 60, SuccessLevel.HardSuccess, SuccessLevel.Failure)]
    [InlineData(96, 99, SuccessLevel.HardSuccess, SuccessLevel.Fumble)]
    [InlineData(96, 100, SuccessLevel.HardSuccess, SuccessLevel.RegularSuccess)]
    [InlineData(96, 249, SuccessLevel.ExtremeSuccess, SuccessLevel.Fumble)]
    [InlineData(96, 250, SuccessLevel.ExtremeSuccess, SuccessLevel.HardSuccess)]
    [InlineData(96, 300, SuccessLevel.CriticalSuccess, SuccessLevel.Fumble)]
    // Уровень считается от полного навыка, а не от урезанного порога
    [InlineData(30, 60, SuccessLevel.HardSuccess, SuccessLevel.HardSuccess)]
    [InlineData(31, 60, SuccessLevel.HardSuccess, SuccessLevel.RegularSuccess)]
    [InlineData(10, 60, SuccessLevel.HardSuccess, SuccessLevel.ExtremeSuccess)]
    [InlineData(1, 60, SuccessLevel.ExtremeSuccess, SuccessLevel.CriticalSuccess)]
    [InlineData(100, 300, SuccessLevel.RegularSuccess, SuccessLevel.Fumble)]
    public void CalculateSuccessLevel_WithDifficulty_FumbleThresholdFollowsTarget(
        int roll, int skill, SuccessLevel difficulty, SuccessLevel expected)
    {
        Assert.Equal(expected, CombatService.CalculateSuccessLevel(roll, skill, difficulty));
    }

    [Theory]
    [Trait("page", "80")]
    [InlineData(60, SuccessLevel.RegularSuccess, 60)]
    [InlineData(60, SuccessLevel.HardSuccess, 30)]
    [InlineData(61, SuccessLevel.HardSuccess, 30)]
    [InlineData(60, SuccessLevel.ExtremeSuccess, 12)]
    [InlineData(64, SuccessLevel.ExtremeSuccess, 12)]
    [InlineData(4, SuccessLevel.ExtremeSuccess, 0)]
    [InlineData(60, SuccessLevel.CriticalSuccess, 1)]
    // Провал и крах как «сложность» — по умолчанию, то есть весь навык
    [InlineData(60, SuccessLevel.Failure, 60)]
    [InlineData(60, SuccessLevel.Fumble, 60)]
    public void GetTargetNumber_ReturnsFloorOfDifficultyShare(int skill, SuccessLevel difficulty, int expected)
    {
        Assert.Equal(expected, CombatService.GetTargetNumber(skill, difficulty));
    }

    [Theory]
    [InlineData(SuccessLevel.CriticalSuccess, "критический успех")]
    [InlineData(SuccessLevel.ExtremeSuccess, "чрезвычайный успех")]
    [InlineData(SuccessLevel.HardSuccess, "трудный успех")]
    [InlineData(SuccessLevel.RegularSuccess, "обычный успех")]
    [InlineData(SuccessLevel.Failure, "провал")]
    [InlineData(SuccessLevel.Fumble, "крах")]
    [InlineData((SuccessLevel)42, "неизвестно")]
    public void GetSuccessLevelText_ReturnsRussianName(SuccessLevel level, string expected)
    {
        Assert.Equal(expected, CombatService.GetSuccessLevelText(level));
    }

    [Theory]
    [Trait("page", "110")]
    [InlineData(RangeLevel.Base, SuccessLevel.RegularSuccess, "обычный")]
    [InlineData(RangeLevel.Long, SuccessLevel.HardSuccess, "трудный")]
    [InlineData(RangeLevel.Extreme, SuccessLevel.ExtremeSuccess, "чрезвычайный")]
    public void GetRequiredLevelForRange_RangeSetsDifficulty(RangeLevel range, SuccessLevel expected, string name)
    {
        Assert.Equal(expected, CombatService.GetRequiredLevelForRange(range));
        Assert.Equal(name, CombatService.GetDifficultyName(range));
        Assert.Equal(name, CombatService.GetDifficultyName(expected));
    }

    [Fact]
    public void GetDifficultyName_Critical_IsCritical()
    {
        Assert.Equal("критический", CombatService.GetDifficultyName(SuccessLevel.CriticalSuccess));
        Assert.Equal("обычный", CombatService.GetDifficultyName(SuccessLevel.Failure));
    }
}
