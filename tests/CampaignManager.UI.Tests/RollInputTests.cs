using Bunit;
using CampaignManager.Core;
using CampaignManager.Core.Dice;
using CampaignManager.UI.Shared;
using Xunit;

namespace CampaignManager.UI.Tests;

public sealed class RollInputTests : KitContext
{
    // Правило листа v1: любой бросок можно вписать с настоящих костей.
    [Fact]
    public void Entered_number_becomes_the_roll()
    {
        D100Roll? roll = null;
        var cut = Render<RollInput>(p => p
            .Add(r => r.Value, 60)
            .Add(r => r.RollChanged, value => roll = value));

        cut.Find("input").Change("26");

        Assert.NotNull(roll);
        Assert.Equal(26, roll.Result);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("101")]
    [InlineData("abc")]
    public void Number_outside_d100_is_rejected(string entered)
    {
        var raised = false;
        var cut = Render<RollInput>(p => p
            .Add(r => r.Value, 60)
            .Add(r => r.RollChanged, _ => raised = true));

        cut.Find("input").Change(entered);

        Assert.False(raised);
        Assert.Contains("от 1 до 100", cut.Find("[role='alert']").TextContent);
    }

    [Fact]
    public void Roll_button_rolls_d100_with_bonus_dice()
    {
        D100Roll? roll = null;
        // Единицы 4, десятки 7 и 2 — с бонусной костью берётся меньшее: 24.
        Dice.Enqueue(4, 7, 2);
        var cut = Render<RollInput>(p => p
            .Add(r => r.Value, 60)
            .Add(r => r.BonusDice, 1)
            .Add(r => r.RollChanged, value => roll = value));

        cut.FindAll("button").Single(b => b.TextContent.Contains("Бросить")).Click();

        Assert.Equal(24, roll?.Result);
    }

    // Уровень считает только Check.Evaluate; компонент его показывает.
    [Theory]
    [InlineData(1, Difficulty.Regular, "Критический успех")]
    [InlineData(12, Difficulty.Regular, "Чрезвычайный успех")]
    [InlineData(26, Difficulty.Regular, "Трудный успех")]
    [InlineData(55, Difficulty.Regular, "Обычный успех")]
    [InlineData(70, Difficulty.Regular, "Провал")]
    [InlineData(100, Difficulty.Regular, "Крах")]
    public void Shows_success_level_of_the_roll(int result, Difficulty difficulty, string expected)
    {
        var cut = Render<RollInput>(p => p
            .Add(r => r.Value, 60)
            .Add(r => r.Difficulty, difficulty)
            .Add(r => r.Roll, D100Roll.Entered(result)));

        Assert.Equal(expected, cut.Find("[data-testid='roll-level']").TextContent.Trim());
    }

    [Fact]
    public void Success_below_required_difficulty_says_what_was_needed()
    {
        var cut = Render<RollInput>(p => p
            .Add(r => r.Value, 60)
            .Add(r => r.Difficulty, Difficulty.Extreme)
            .Add(r => r.Roll, D100Roll.Entered(26)));

        Assert.Contains("нужен чрезвычайный успех", cut.Markup);
        Assert.Contains("нужно ≤ 12", cut.Markup);
    }
}
