using CampaignManager.Web.Components.Features.Characters.Model;
using CampaignManager.Web.Components.Features.Checks.Model;
using CampaignManager.Web.Components.Features.Checks.Services;

namespace CampaignManager.Rules.Tests.Checks;

/// <summary>
///     Цель проверки по имени из проверки локации (<see cref="CheckTargets.ResolveByName" />).
///     Страницы у сопоставления имени в коде v1 нет.
/// </summary>
[Trait("page", "?")]
public sealed class CheckTargetsTests
{
    private static Character Sheet()
    {
        var sheet = new Character { Skills = SkillsModel.DefaultSkillsModel() };
        sheet.DerivedAttributes.Luck = new AttributeWithMaxValue(55, 99);
        sheet.Characteristics.Strength = new AttributeValue(65);
        return sheet;
    }

    [Theory]
    [InlineData("Удача", CheckTargetKind.Luck, "Удача", 55)]
    [InlineData("удача", CheckTargetKind.Luck, "Удача", 55)]
    [InlineData("СИЛ", CheckTargetKind.Characteristic, "СИЛ", 65)]
    [InlineData("сил", CheckTargetKind.Characteristic, "СИЛ", 65)]
    [InlineData("Сила", CheckTargetKind.Characteristic, "СИЛ", 65)]
    [InlineData("  Сила ", CheckTargetKind.Characteristic, "СИЛ", 65)]
    [InlineData("Образование", CheckTargetKind.Characteristic, "ОБР", 75)]
    [InlineData("Внимание", CheckTargetKind.Skill, "Внимание", 25)]
    // Не точное имя — ищет разбор боя, а подпись остаётся как в локации
    [InlineData("Стрельба (П)", CheckTargetKind.Skill, "Стрельба (П)", 20)]
    [InlineData("Ближний бой", CheckTargetKind.Skill, "Ближний бой", 25)]
    [InlineData("внимание", CheckTargetKind.Skill, "внимание", 25)]
    [InlineData("Несуществующий навык", CheckTargetKind.Skill, "Несуществующий навык", 0)]
    // Рассудка среди целей нет: это «навык» со значением 0
    [InlineData("Рассудок", CheckTargetKind.Skill, "Рассудок", 0)]
    public void ResolveByName(string name, CheckTargetKind kind, string expectedName, int value)
    {
        Assert.Equal(new CheckTarget(kind, expectedName, value), CheckTargets.ResolveByName(Sheet(), name));
    }

    [Fact]
    public void FindByKey_RoundTripsKeys()
    {
        var sheet = Sheet();

        Assert.Equal(CheckTargets.Luck(sheet), CheckTargets.FindByKey(sheet, "luck"));
        Assert.Equal(65, CheckTargets.FindByKey(sheet, "char:СИЛ")!.Value);
        Assert.Equal(20, CheckTargets.FindByKey(sheet, "skill:Слух")!.Value);
        Assert.Null(CheckTargets.FindByKey(sheet, "skill:Стрельба (П)")); // по ключу — только точное имя
        Assert.Null(CheckTargets.FindByKey(sheet, "other"));
    }

    [Fact]
    public void Characteristics_EightInSheetOrder()
    {
        var targets = CheckTargets.Characteristics(Sheet());

        Assert.Equal(["СИЛ", "ВЫН", "ТЕЛ", "ЛВК", "НАР", "ИНТ", "МОЩ", "ОБР"], targets.Select(t => t.Name));
        Assert.All(targets, t => Assert.Equal(CheckTargetKind.Characteristic, t.Kind));
    }
}
