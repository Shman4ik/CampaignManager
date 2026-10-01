using CampaignManager.Web.Components.Features.Characters.Model;
using CampaignManager.Web.Components.Features.Characters.Services;
using static CampaignManager.Rules.Tests.Sheet.Sheets;

namespace CampaignManager.Rules.Tests.Sheet;

/// <summary>Вторичные атрибуты главы 3: ПЗ, ПМ, Уклонение, Скорость, таблица I.</summary>
public sealed class DerivedAttributeRulesTests
{
    [Theory]
    [Trait("page", "30")]
    [InlineData(50, 50, 10)]
    [InlineData(55, 64, 11)] // 119 / 10 — вниз
    [InlineData(40, 49, 8)]
    [InlineData(90, 99, 18)]
    [InlineData(15, 15, 3)]
    public void ComputeMaxHitPoints_SizPlusConDividedByTen_RoundsDown(int siz, int con, int expected) =>
        Assert.Equal(expected, DerivedAttributeRules.ComputeMaxHitPoints(Chars(siz: siz, con: con)));

    [Theory]
    [Trait("page", "31")]
    [InlineData(1, 0)]
    [InlineData(50, 10)]
    [InlineData(54, 10)]
    [InlineData(55, 11)]
    [InlineData(99, 19)]
    public void ComputeMaxMagicPoints_PowDividedByFive_RoundsDown(int pow, int expected) =>
        Assert.Equal(expected, DerivedAttributeRules.ComputeMaxMagicPoints(Chars(pow: pow)));

    [Theory]
    [Trait("page", "57")]
    [InlineData(1, 0)]
    [InlineData(45, 22)]
    [InlineData(50, 25)]
    [InlineData(99, 49)]
    public void ComputeDodge_HalfDex_RoundsDown(int dex, int expected) =>
        Assert.Equal(expected, DerivedAttributeRules.ComputeDodge(Chars(dex: dex)));

    /// <summary>Таблица I: каждая граница с обеих сторон, затем шаг 80 за 444.</summary>
    [Theory]
    [Trait("page", "31")]
    [InlineData(0, "-2", "-2")]
    [InlineData(2, "-2", "-2")]
    [InlineData(64, "-2", "-2")]
    [InlineData(65, "-1", "-1")]
    [InlineData(84, "-1", "-1")]
    [InlineData(85, "0", "0")]
    [InlineData(124, "0", "0")]
    [InlineData(125, "1", "+1D4")]
    [InlineData(164, "1", "+1D4")]
    [InlineData(165, "2", "+1D6")]
    [InlineData(204, "2", "+1D6")]
    [InlineData(205, "3", "+2D6")]
    [InlineData(284, "3", "+2D6")]
    [InlineData(285, "4", "+3D6")]
    [InlineData(364, "4", "+3D6")]
    [InlineData(365, "5", "+4D6")]
    [InlineData(444, "5", "+4D6")]
    [InlineData(445, "6", "+5D6")]
    [InlineData(524, "6", "+5D6")]
    [InlineData(525, "7", "+6D6")]
    [InlineData(604, "7", "+6D6")]
    [InlineData(605, "8", "+7D6")]
    public void ComputeBuildAndDamageBonus_StrPlusSiz_FollowsTableOne(int sum, string build, string damageBonus)
    {
        var str = sum / 2;
        var (actualBuild, actualBonus) =
            DerivedAttributeRules.ComputeBuildAndDamageBonus(Chars(str: str, siz: sum - str));

        Assert.Equal(build, actualBuild);
        Assert.Equal(damageBonus, actualBonus);
    }

    [Theory]
    [Trait("page", "31")]
    [InlineData(40, 40, 50, 7)] // обе меньше ТЕЛ
    [InlineData(40, 60, 50, 8)]
    [InlineData(60, 40, 50, 8)]
    [InlineData(50, 40, 50, 8)] // СИЛ равна ТЕЛ
    [InlineData(50, 50, 50, 8)] // обе равны
    [InlineData(60, 50, 50, 8)] // ЛВК равна ТЕЛ — ещё не 9
    [InlineData(60, 60, 50, 9)] // обе больше ТЕЛ
    public void ComputeMoveRate_YoungInvestigator_ComparesStrAndDexWithSiz(int str, int dex, int siz, int expected) =>
        Assert.Equal(expected, DerivedAttributeRules.ComputeMoveRate(Chars(str: str, dex: dex, siz: siz), 25));

    /// <summary>Скорость 8 и минус 1 за каждое десятилетие начиная с 40 лет.</summary>
    [Theory]
    [Trait("page", "31")]
    [InlineData(15, 8)]
    [InlineData(39, 8)]
    [InlineData(40, 7)]
    [InlineData(49, 7)]
    [InlineData(50, 6)]
    [InlineData(59, 6)]
    [InlineData(60, 5)]
    [InlineData(69, 5)]
    [InlineData(70, 4)]
    [InlineData(79, 4)]
    [InlineData(80, 3)]
    [InlineData(89, 3)]
    [InlineData(90, 2)] // за пределами таблицы возраста формула просто продолжает счёт
    public void ComputeMoveRate_ByAge_LosesOnePerDecadeFromForty(int age, int expected) =>
        Assert.Equal(expected, DerivedAttributeRules.ComputeMoveRate(Chars(str: 50, dex: 50, siz: 50), age));

    [Fact]
    [Trait("page", "31")]
    public void ComputeMoveRate_VeryOld_NeverBelowOne() =>
        Assert.Equal(1, DerivedAttributeRules.ComputeMoveRate(Chars(str: 40, dex: 40, siz: 50), 120));

    /// <summary>
    ///     Штраф Скорости за возраст объявлен дважды (AUDIT): в формуле и в таблице возраста.
    ///     На всём допустимом возрасте они обязаны совпадать.
    /// </summary>
    [Fact]
    [Trait("page", "30-31")]
    public void ComputeMoveRate_AllCreationAges_MatchesAgeBandMovePenalty()
    {
        var c = Chars(str: 50, dex: 50, siz: 50);
        for (var age = InvestigatorCreationRules.MinAge; age <= InvestigatorCreationRules.MaxAge; age++)
            Assert.Equal(8 - InvestigatorCreationRules.BandFor(age).MovePenalty,
                DerivedAttributeRules.ComputeMoveRate(c, age));
    }

    [Fact]
    [Trait("page", "93")]
    public void MaxLuck_Is99_SameAsDevelopmentPhase()
    {
        Assert.Equal(99, DerivedAttributeRules.MaxLuck);
        Assert.Equal(DerivedAttributeRules.MaxLuck, DevelopmentPhaseRules.MaxLuck);
    }

    [Fact]
    [Trait("page", "30-31")]
    public void Recalculate_ClampsCurrentToNewMaximums_AndFillsDerivedFields()
    {
        var character = WithMythos(20, sanity: 90);
        character.Characteristics = Chars(str: 70, con: 50, siz: 60, dex: 65, pow: 40);
        character.PersonalInfo.Age = 45;
        character.DerivedAttributes.HitPoints = new AttributeWithMaxValue(30, 30);
        character.DerivedAttributes.MagicPoints = new AttributeWithMaxValue(5, 20);
        character.DerivedAttributes.Luck = new AttributeWithMaxValue(120, 60);

        DerivedAttributeRules.Recalculate(character);

        var d = character.DerivedAttributes;
        Assert.Equal((11, 11), (d.HitPoints.Value, d.HitPoints.MaxValue));
        Assert.Equal((5, 8), (d.MagicPoints.Value, d.MagicPoints.MaxValue)); // текущие ПМ не поднимаются
        Assert.Equal((99, 99), (d.Luck.Value, d.Luck.MaxValue));
        Assert.Equal((79, 79), (d.Sanity.Value, d.Sanity.MaxValue)); // 99 − Мифы 20
        Assert.Equal("1", character.PersonalInfo.Build); // 70 + 60 = 130
        Assert.Equal("+1D4", character.PersonalInfo.DamageBonus);
        Assert.Equal(8, character.PersonalInfo.MoveSpeed); // 9 минус 1 за 45 лет
        Assert.Equal(32, character.PersonalInfo.Dodge); // навыка Уклонение нет — половина ЛВК
    }

    [Fact]
    [Trait("page", "30")]
    public void Recalculate_RefreshesHalfAndFifthOfCharacteristics()
    {
        var character = Character();
        character.Characteristics.Strength.Regular = 73;

        DerivedAttributeRules.Recalculate(character);

        Assert.Equal(36, character.Characteristics.Strength.Half);
        Assert.Equal(14, character.Characteristics.Strength.Fifth);
    }

    [Fact]
    [Trait("page", "31")]
    public void InitializeNewSheet_FillsHpMpToMax_AndSanityEqualsPow()
    {
        var character = Character(sanity: 0);
        character.Characteristics = Chars(con: 50, siz: 60, pow: 65);

        DerivedAttributeRules.InitializeNewSheet(character);

        Assert.Equal(11, character.DerivedAttributes.HitPoints.Value);
        Assert.Equal(13, character.DerivedAttributes.MagicPoints.Value);
        Assert.Equal(65, character.DerivedAttributes.Sanity.Value);
    }

    [Fact]
    [Trait("page", "31")]
    public void InitializeNewSheet_PowAboveMaxSanity_SanityCappedByMythos()
    {
        var character = WithMythos(40, sanity: 0);
        character.Characteristics = Chars(pow: 80);

        DerivedAttributeRules.InitializeNewSheet(character);

        Assert.Equal(59, character.DerivedAttributes.Sanity.Value);
    }

    [Theory]
    [Trait("page", "93")]
    [InlineData(60, 60, 60)]
    [InlineData(120, 120, 99)]
    public void NormalizeLuckCap_SetsMaxTo99_ClampsValue(int value, int max, int expectedValue)
    {
        var character = Character();
        character.DerivedAttributes.Luck = new AttributeWithMaxValue(value, max);

        DerivedAttributeRules.NormalizeLuckCap(character);

        Assert.Equal(expectedValue, character.DerivedAttributes.Luck.Value);
        Assert.Equal(99, character.DerivedAttributes.Luck.MaxValue);
    }

    [Theory]
    [Trait("page", "57")]
    [InlineData(10, 22, 22)] // навык ниже базы — поднимается до половины ЛВК
    [InlineData(40, 22, 40)] // вложенные пункты не трогаются
    public void ApplyDodge_WithSkill_RaisesOnlyUpToBase_AndMirrorsSkill(int skillValue, int dodge, int expected)
    {
        var character = Character(50, Skill(Dodge, skillValue));

        DerivedAttributeRules.ApplyDodge(character, dodge);

        Assert.Equal(expected, Find(character, Dodge).Value.Regular);
        Assert.Equal(expected, character.PersonalInfo.Dodge);
    }

    [Fact]
    [Trait("page", "57")]
    public void SyncDodgeFromSkill_CopiesSkillValue_WithoutSkillLeavesFieldAlone()
    {
        var withSkill = Character(50, Skill(Dodge, 47));
        DerivedAttributeRules.SyncDodgeFromSkill(withSkill);
        Assert.Equal(47, withSkill.PersonalInfo.Dodge);

        var without = Character();
        without.PersonalInfo.Dodge = 13;
        DerivedAttributeRules.SyncDodgeFromSkill(without);
        Assert.Equal(13, without.PersonalInfo.Dodge);
    }

    [Fact]
    [Trait("page", "57")]
    public void FindDodgeSkill_MatchesNameExactly()
    {
        Assert.Null(DerivedAttributeRules.FindDodgeSkill(Character(50, Skill("уклонение", 30))));
        Assert.NotNull(DerivedAttributeRules.FindDodgeSkill(Character(50, Skill(Dodge, 30))));
    }
}
