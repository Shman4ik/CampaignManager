using CampaignManager.Core.Characters;
using CampaignManager.Core.Checks;

namespace CampaignManager.Core.Tests.Checks;

/// <summary>Проверка Идеи (гл. 10): сложность — по тому, как подавали пропущенную зацепку; бросает сыщик с наибольшим ИНТ.</summary>
public sealed class IdeaCheckTests
{
    [Theory]
    [Trait("page", "197")]
    [InlineData(IdeaClue.NotMentioned, Difficulty.Regular)]
    [InlineData(IdeaClue.Mentioned, Difficulty.Hard)]
    [InlineData(IdeaClue.Emphasized, Difficulty.Extreme)]
    public void Difficulty_GrowsWithHowClearTheClueWas(IdeaClue clue, Difficulty difficulty) =>
        Assert.Equal(difficulty, IdeaCheck.DifficultyOf(clue));

    [Fact]
    [Trait("page", "197")]
    public void Roller_HighestIntAlive_FirstOnTie()
    {
        var harvey = Sheet(60);
        var roger = Sheet(75);
        var cecil = Sheet(75);
        var dead = Sheet(90, dead: true);

        Assert.Same(roger, IdeaCheck.Roller([harvey, roger, cecil, dead], s => s));
        Assert.Null(IdeaCheck.Roller([dead], s => s));
        Assert.Null(IdeaCheck.Roller(Array.Empty<CharacterSheet>(), s => s));
    }

    [Fact]
    public void SubjectKey_IsIntelligenceCheck() =>
        Assert.Equal(IdeaCheck.SubjectKey, new CheckSubject(CheckSubjectKind.Characteristic, "ИНТ", 60) { Characteristic = Characteristic.INT }.Key);

    private static CharacterSheet Sheet(int intelligence, bool dead = false) => new()
    {
        Characteristics = new Characteristics { Int = intelligence },
        Condition = new SheetCondition { Dead = dead },
    };
}
