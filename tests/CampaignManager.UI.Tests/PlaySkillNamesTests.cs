using CampaignManager.UI.Characters;
using Xunit;

namespace CampaignManager.UI.Tests;

public sealed class PlaySkillNamesTests
{
    [Theory]
    [InlineData("Управление тяжёлыми машинами", "Тяж. машины")]
    [InlineData("Стрельба (пистолет-пулемёт)", "Стрельба (ПП)")]
    [InlineData("Искусство/ремесло (актёрская игра)", "Иск./ремесло (актёрская игра)")]
    [InlineData("Язык, иностранный (латынь)", "Язык (латынь)")]
    [InlineData("Внимание", "Внимание")]
    [InlineData("Язык, родной", "Язык, родной")]
    public void Long_names_are_shortened_and_short_ones_kept(string name, string expected) =>
        Assert.Equal(expected, PlaySkillNames.Short(name));
}
