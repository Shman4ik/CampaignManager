using CampaignManager.Core.Catalogs;
using CampaignManager.Core.Characters;
using CampaignManager.Core.Dice;
using CampaignManager.Core.Tests.Infrastructure;
using static CampaignManager.Core.Tests.Sheet.Sheets;

namespace CampaignManager.Core.Tests.Sheet;

/// <summary>Помощник над черновиком (T2.4): правила шагов, которые в v1 жили в разметке (AUDIT, «Правила в разметке»).</summary>
public sealed class CreationPlanTests
{
    private static InvestigatorDraft Filled(int edu = 60)
    {
        var draft = new InvestigatorDraft { Age = 25 };
        foreach (var key in Enum.GetValues<Characteristic>())
            draft.SetCharacteristic(key, key is Characteristic.EDU ? edu : 50);
        return draft;
    }

    private static OccupationDefinition Criminal() => new(Guid.NewGuid(), "Преступник", SkillPointsFormula.Edu2DexOrStr2)
    {
        CreditRatingMin = 5,
        CreditRatingMax = 65,
        Slots =
        [
            new OccupationSlotDefinition(OccupationSlotKind.Skill) { SkillId = Id("Психология") },
            new OccupationSlotDefinition(OccupationSlotKind.Choice) { ChooseCount = 2, Options = [Id(Fighting), Id("Взлом")] },
        ],
    };

    [Fact]
    [Trait("page", "38")]
    public void OptionsFor_ChoiceGroup_SharesPool()
    {
        var occupation = Criminal();
        var draft = Filled();
        CreationPlan.SelectOccupation(draft, occupation);
        var plan = new CreationPlan(draft, Catalog, occupation);
        plan.SyncSlots();

        var brawl = CreationPlan.KeyOf(Id("Ближний бой (драка)"));
        plan.SetChoice(1, brawl);

        Assert.DoesNotContain(brawl, plan.OptionsFor(2));
        Assert.Contains(brawl, plan.OptionsFor(1));
        Assert.Contains(CreationPlan.KeyOf(Id("Взлом")), plan.OptionsFor(2));
    }

    [Fact]
    [Trait("page", "38-39")]
    public void FormulaChoice_Required_AndChosenCharacteristicCounts()
    {
        var occupation = Criminal();
        var draft = Filled();
        draft.SetCharacteristic(Characteristic.STR, 80);
        CreationPlan.SelectOccupation(draft, occupation);
        var plan = new CreationPlan(draft, Catalog, occupation);
        plan.SyncSlots();
        plan.SetChoice(1, CreationPlan.KeyOf(Id("Ближний бой (драка)")));
        plan.SetChoice(2, CreationPlan.KeyOf(Id("Взлом")));

        Assert.Equal("Выберите характеристику в формуле очков профессии", plan.Validate(CreationStep.Occupation));

        draft.FormulaChoice = Characteristic.DEX;
        Assert.Null(plan.Validate(CreationStep.Occupation));
        Assert.Equal(60 * 2 + 50 * 2, plan.OccupationBudget); // выбранная ЛВК, а не большая СИЛ
    }

    [Fact]
    [Trait("page", "52")]
    public void AddSpecialization_OwnOrCatalog()
    {
        var occupation = Criminal();
        var draft = Filled();
        CreationPlan.SelectOccupation(draft, occupation);
        var plan = new CreationPlan(draft, Catalog, occupation);
        plan.SyncSlots();

        Assert.Equal(CreationPlan.KeyOf(Id("Ближний бой (меч)")), plan.AddSpecialization(1, Id(Fighting), "Меч"));
        Assert.Equal("кнут", plan.AddSpecialization(2, Id(Fighting), " Кнут "));
        Assert.Equal(Id(Fighting), draft.AddedSpecializations["кнут"]);
        Assert.Equal("Ближний бой (кнут)", plan.NameOf("кнут"));
        Assert.Equal(25, plan.BaseOf("кнут")); // база первой соседней — драки

        plan.SetChoice(2, CreationPlan.KeyOf(Id("Взлом")));
        Assert.False(draft.AddedSpecializations.ContainsKey("кнут")); // своя специализация живёт, пока её держит слот
    }

    [Fact]
    [Trait("page", "46")]
    public void ClampPoints_OptionalCap()
    {
        var draft = Filled();
        draft.SkillCap = InvestigatorCreationRules.OptionalSkillCap;
        var plan = new CreationPlan(draft, Catalog, null);
        var spot = CreationPlan.KeyOf(Id("Внимание"));

        plan.SetPersonalPoints(spot, 80);

        Assert.Equal(50, draft.PersonalPoints[spot]); // 25 база + 50 = 75
        Assert.Equal(75, plan.Total(spot));
    }

    [Fact]
    [Trait("page", "34")]
    public void Mythos_NotBoughtWithPoints()
    {
        var plan = new CreationPlan(Filled(), Catalog, null);
        var mythos = CreationPlan.KeyOf(Id(Mythos));

        plan.SetPersonalPoints(mythos, 10);

        Assert.False(plan.CanTakePoints(mythos));
        Assert.Empty(plan.Draft.PersonalPoints);
    }

    [Fact]
    [Trait("page", "46")]
    public void Blitz_ValueReplacesBase_CreditFromSameSet()
    {
        var occupation = Criminal();
        var draft = Filled();
        draft.SetMethod(CreationMethod.Blitz);
        CreationPlan.SelectOccupation(draft, occupation);
        var plan = new CreationPlan(draft, Catalog, occupation);
        plan.SyncSlots();
        var psychology = CreationPlan.KeyOf(Id("Психология"));

        plan.SetBlitzValue(psychology, 70);
        plan.SetBlitzValue(CreationPlan.KeyOf(Id(CreditRating)), 40);

        Assert.Equal(70, plan.Total(psychology));
        Assert.Equal(40, draft.CreditRating);
        Assert.DoesNotContain(70, plan.BlitzAvailableFor(CreationPlan.KeyOf(Id("Взлом"))));
    }

    [Fact]
    [Trait("page", "30")]
    public void EducationCheck_EnteredDice_SameRule()
    {
        var draft = Filled(edu: 60);

        var check = draft.AddEducationCheck(new EnteredDiceRoller(new ScriptedDice()).Percentile(75).Total(7, 1, 10));

        Assert.Equal(new EducationCheck(75, 60, 7), check);
        Assert.Equal(67, draft.Value(Characteristic.EDU, InvestigatorCreationRules.BandFor(25)));
        Assert.Null(draft.AddEducationCheck(DiceRoller.Shared)); // «Молодому» положена одна проверка

        draft.SetCharacteristic(Characteristic.EDU, 65);
        Assert.Empty(draft.EducationChecks); // проверки бросались против прежнего ОБР
    }

    [Fact]
    [Trait("page", "46")]
    public void Pool_EditingAssignedValue_Unassigns()
    {
        var draft = new InvestigatorDraft();
        draft.SetMethod(CreationMethod.AssignRolls);
        draft.SetPoolValue(0, 65);
        draft.SetPoolValue(1, 65);
        draft.SetCharacteristic(Characteristic.STR, 65);

        Assert.Equal([65], draft.AvailableFor(Characteristic.DEX));

        draft.SetPoolValue(0, 70);
        Assert.False(draft.Rolled.ContainsKey(Characteristic.STR));
    }

    [Fact]
    [Trait("page", "30")]
    public void Luck_YoungRollsTwice_TakesBest()
    {
        var draft = new InvestigatorDraft();
        draft.SetAge(17);

        draft.RollLuck(new ScriptedDice(2, 2, 2, 6, 6, 6));

        Assert.Equal([30, 90], draft.LuckRolls);
        Assert.Equal(90, draft.Luck);
    }

    [Theory]
    [Trait("page", "28-29")]
    [InlineData(Characteristic.STR, 13, 65)]
    [InlineData(Characteristic.INT, 10, 80)]
    public void FromDiceSum_AndBack(Characteristic key, int sum, int value)
    {
        Assert.Equal(value, InvestigatorCreationRules.FromDiceSum(key, sum));
        Assert.Equal(sum, InvestigatorCreationRules.DiceSumOf(key, value));
    }

    [Fact]
    public void Method_Step_RequiresNameThenAge()
    {
        var draft = Filled();
        var plan = new CreationPlan(draft, Catalog, null);

        Assert.Equal("Впишите имя сыщика", plan.Validate(CreationStep.Method));

        draft.Personal.Name = "  ";
        Assert.Equal("Впишите имя сыщика", plan.Validate(CreationStep.Method));

        draft.Personal.Name = "Артур Морган";
        Assert.Null(plan.Validate(CreationStep.Method));
        Assert.Null(plan.Validate(CreationStep.Biography)); // имя больше не держит биографию
    }
}
