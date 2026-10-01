using CampaignManager.Web.Components.Features.Combat.Model;
using CampaignManager.Web.Components.Features.Combat.Services;

namespace CampaignManager.Rules.Tests.Dice;

/// <summary>Залп и нарастающая сложность автоматической стрельбы.</summary>
public sealed class AutofireTests
{
    [Theory]
    [Trait("page", "114")]
    // Первая проверка в раунде — как обычно
    [InlineData(SuccessLevel.RegularSuccess, 0, SuccessLevel.RegularSuccess, false, 0)]
    [InlineData(SuccessLevel.RegularSuccess, -1, SuccessLevel.RegularSuccess, false, 0)]
    // Вторая и третья — штрафные кости
    [InlineData(SuccessLevel.RegularSuccess, 1, SuccessLevel.RegularSuccess, false, 1)]
    [InlineData(SuccessLevel.RegularSuccess, 2, SuccessLevel.RegularSuccess, false, 2)]
    // Дальше штрафных остаётся две, сложность растёт на уровень
    [InlineData(SuccessLevel.RegularSuccess, 3, SuccessLevel.HardSuccess, false, 2)]
    [InlineData(SuccessLevel.RegularSuccess, 4, SuccessLevel.ExtremeSuccess, false, 2)]
    [InlineData(SuccessLevel.RegularSuccess, 5, SuccessLevel.CriticalSuccess, false, 2)]
    [InlineData(SuccessLevel.RegularSuccess, 6, SuccessLevel.CriticalSuccess, true, 2)]
    // Большая дальность стартует с трудного
    [InlineData(SuccessLevel.HardSuccess, 0, SuccessLevel.HardSuccess, false, 0)]
    [InlineData(SuccessLevel.HardSuccess, 3, SuccessLevel.ExtremeSuccess, false, 2)]
    [InlineData(SuccessLevel.HardSuccess, 4, SuccessLevel.CriticalSuccess, false, 2)]
    [InlineData(SuccessLevel.HardSuccess, 5, SuccessLevel.CriticalSuccess, true, 2)]
    [InlineData(SuccessLevel.ExtremeSuccess, 3, SuccessLevel.CriticalSuccess, false, 2)]
    [InlineData(SuccessLevel.ExtremeSuccess, 4, SuccessLevel.CriticalSuccess, true, 2)]
    public void EscalateAutofire_PenaltyDiceThenDifficulty(
        SuccessLevel baseRequired, int checkIndex,
        SuccessLevel required, bool impossible, int extraPenalty)
    {
        var (actualRequired, actualImpossible, actualPenalty) =
            CombatService.EscalateAutofire(baseRequired, checkIndex);

        Assert.Equal(required, actualRequired);
        Assert.Equal(impossible, actualImpossible);
        Assert.Equal(extraPenalty, actualPenalty);
    }

    [Theory]
    [Trait("page", "112")]
    [InlineData(0, 3)]
    [InlineData(29, 3)]
    [InlineData(39, 3)]
    [InlineData(40, 4)]
    [InlineData(55, 5)]
    [InlineData(99, 9)]
    public void GetVolleySize_SkillOverTenButAtLeastThree(int skill, int expected)
    {
        Assert.Equal(expected, CombatService.GetVolleySize(skill));
    }
}
