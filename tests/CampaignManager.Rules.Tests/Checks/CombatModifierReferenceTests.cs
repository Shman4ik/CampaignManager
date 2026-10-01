using CampaignManager.Web.Components.Features.Combat.Model;
using CampaignManager.Web.Components.Features.KeeperScreen.Services;

namespace CampaignManager.Rules.Tests.Checks;

/// <summary>
///     Снимок памятки модификаторов на ширме Хранителя. Каждая строка получена вопросом к
///     <c>CombatService.CalculateAttackModifiers</c> с одним включённым условием, поэтому у каждой —
///     ровно одна причина.
/// </summary>
[Trait("page", "105-114")]
public sealed class CombatModifierReferenceTests
{
    [Fact]
    public void Firearms_Snapshot_OneReasonPerRow()
    {
        var rows = CombatModifierReference.Firearms();

        Assert.Equal(
            [
                "+1 стрельба в упор",
                "+1 прицеливание",
                "+1 стрельба лёжа",
                "+1 крупная цель",
                "−1 цель укрылась от огня",
                "−1 частичное укрытие",
                "−1 быстро движущаяся цель",
                "−1 стрельба в ближнем бою",
                "−1 серия выстрелов",
                "−1 зарядка и выстрел",
                "−1 мелкая цель",
                "−1 цель лежит",
                "−1 проверка №2 при автоматической стрельбе"
            ],
            rows.Select(r => r.Effect));
        AssertOneReasonEach(rows);
    }

    [Fact]
    public void Melee_Snapshot_OneReasonPerRow()
    {
        var rows = CombatModifierReference.Melee();

        Assert.Equal(
            ["+1 цель застигнута врасплох", "+1 цель повалена", "+1 численное превосходство"],
            rows.Select(r => r.Effect));
        AssertOneReasonEach(rows);
    }

    [Fact]
    [Trait("page", "110")]
    public void Ranges_Snapshot()
    {
        var rows = CombatModifierReference.Ranges();

        Assert.Equal(
            [
                (SuccessLevel.RegularSuccess, "обычный"),
                (SuccessLevel.HardSuccess, "трудный"),
                (SuccessLevel.ExtremeSuccess, "чрезвычайный")
            ],
            rows.Select(r => (r.Required, r.DifficultyName)));
    }

    private static void AssertOneReasonEach(IReadOnlyList<DiceModifierRow> rows) =>
        Assert.All(rows, row =>
        {
            Assert.NotEqual("—", row.Effect);
            Assert.DoesNotContain("; ", row.Effect, StringComparison.Ordinal);
            Assert.False(string.IsNullOrWhiteSpace(row.Condition));
        });
}
