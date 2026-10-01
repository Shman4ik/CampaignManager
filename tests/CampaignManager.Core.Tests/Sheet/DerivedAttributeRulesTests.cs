using CampaignManager.Core.Characters;
using static CampaignManager.Core.Tests.Sheet.Sheets;

namespace CampaignManager.Core.Tests.Sheet;

/// <summary>
/// Вторичные атрибуты главы 3: ПЗ, ПМ, Уклонение, Скорость, таблица I. Перенесено из T0.2 без правки
/// ожиданий. Хранимых производных в листе 2.0 нет, поэтому тесты v1 на <c>Recalculate</c> (заполнение
/// полей листа), <c>NormalizeLuckCap</c> и зеркало Уклонения стали тестами <see cref="DerivedAttributeRules.Compute"/>
/// и <see cref="DerivedAttributeRules.Normalize"/> на тех же числах.
/// </summary>
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
    [InlineData(0, -2, "-2")]
    [InlineData(2, -2, "-2")]
    [InlineData(64, -2, "-2")]
    [InlineData(65, -1, "-1")]
    [InlineData(84, -1, "-1")]
    [InlineData(85, 0, "0")]
    [InlineData(124, 0, "0")]
    [InlineData(125, 1, "+1D4")]
    [InlineData(164, 1, "+1D4")]
    [InlineData(165, 2, "+1D6")]
    [InlineData(204, 2, "+1D6")]
    [InlineData(205, 3, "+2D6")]
    [InlineData(284, 3, "+2D6")]
    [InlineData(285, 4, "+3D6")]
    [InlineData(364, 4, "+3D6")]
    [InlineData(365, 5, "+4D6")]
    [InlineData(444, 5, "+4D6")]
    [InlineData(445, 6, "+5D6")]
    [InlineData(524, 6, "+5D6")]
    [InlineData(525, 7, "+6D6")]
    [InlineData(604, 7, "+6D6")]
    [InlineData(605, 8, "+7D6")]
    public void ComputeBuildAndDamageBonus_StrPlusSiz_FollowsTableOne(int sum, int build, string damageBonus)
    {
        var str = sum / 2;
        var (actualBuild, actualBonus) = DerivedAttributeRules.ComputeBuildAndDamageBonus(Chars(str: str, siz: sum - str));

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

    /// <summary>Штраф Скорости за возраст объявлен дважды (AUDIT): в формуле и в таблице возраста.</summary>
    [Fact]
    [Trait("page", "30-31")]
    public void ComputeMoveRate_AllCreationAges_MatchesAgeBandMovePenalty()
    {
        var c = Chars(str: 50, dex: 50, siz: 50);
        for (var age = InvestigatorCreationRules.MinAge; age <= InvestigatorCreationRules.MaxAge; age++)
            Assert.Equal(8 - InvestigatorCreationRules.BandFor(age).MovePenalty, DerivedAttributeRules.ComputeMoveRate(c, age));
    }

    [Fact]
    [Trait("page", "93")]
    public void MaxLuck_Is99() => Assert.Equal(99, DerivedAttributeRules.MaxLuck);

    /// <summary>Бывший <c>Recalculate_ClampsCurrentToNewMaximums_AndFillsDerivedFields</c>: те же числа.</summary>
    [Fact]
    [Trait("page", "30-31")]
    public void Compute_AndNormalize_ClampCurrentToNewMaximums()
    {
        var sheet = WithMythos(20, sanity: 90);
        sheet.Characteristics = Chars(str: 70, con: 50, siz: 60, dex: 65, pow: 40);
        sheet.Personal.Age = 45;
        sheet.Current.HitPoints = 30;
        sheet.Current.MagicPoints = 5;
        sheet.Current.Luck = 120;

        DerivedAttributeRules.Normalize(sheet, Catalog);
        var derived = DerivedAttributeRules.Compute(sheet, Catalog);

        Assert.Equal((11, 11), (sheet.Current.HitPoints, derived.MaxHitPoints));
        Assert.Equal((5, 8), (sheet.Current.MagicPoints, derived.MaxMagicPoints)); // текущие ПМ не поднимаются
        Assert.Equal((99, 99), (sheet.Current.Luck, derived.MaxLuck));
        Assert.Equal((79, 79), (sheet.Current.Sanity, derived.MaxSanity)); // 99 − Мифы 20
        Assert.Equal(1, derived.Build); // 70 + 60 = 130
        Assert.Equal("+1D4", derived.DamageBonus);
        Assert.Equal(8, derived.Move); // 9 минус 1 за 45 лет
        Assert.Equal(32, derived.Dodge); // навыка Уклонение на листе нет — половина ЛВК
    }

    [Fact]
    [Trait("page", "30")]
    public void Half_AndFifth_RoundDown()
    {
        Assert.Equal(36, CharacteristicMath.Half(73));
        Assert.Equal(14, CharacteristicMath.Fifth(73));
    }

    [Fact]
    [Trait("page", "31")]
    public void InitializeNewSheet_FillsHpMpToMax_AndSanityEqualsPow()
    {
        var sheet = NewSheet(sanity: 0);
        sheet.Characteristics = Chars(con: 50, siz: 60, pow: 65);

        DerivedAttributeRules.InitializeNewSheet(sheet, Catalog);

        Assert.Equal(11, sheet.Current.HitPoints);
        Assert.Equal(13, sheet.Current.MagicPoints);
        Assert.Equal(65, sheet.Current.Sanity);
    }

    [Fact]
    [Trait("page", "31")]
    public void InitializeNewSheet_PowAboveMaxSanity_SanityCappedByMythos()
    {
        var sheet = WithMythos(40, sanity: 0);
        sheet.Characteristics = Chars(pow: 80);

        DerivedAttributeRules.InitializeNewSheet(sheet, Catalog);

        Assert.Equal(59, sheet.Current.Sanity);
    }

    [Theory]
    [Trait("page", "93")]
    [InlineData(60, 60)]
    [InlineData(120, 99)]
    public void Normalize_LuckCappedAt99(int luck, int expected)
    {
        var sheet = NewSheet();
        sheet.Current.Luck = luck;

        DerivedAttributeRules.Normalize(sheet, Catalog);

        Assert.Equal(expected, sheet.Current.Luck);
        Assert.Equal(99, DerivedAttributeRules.Compute(sheet, Catalog).MaxLuck);
    }

    [Theory]
    [Trait("page", "57")]
    [InlineData(10, 22)] // навык ниже базы — поднимается до половины ЛВК 45
    [InlineData(40, 40)] // вложенные пункты не трогаются
    public void Normalize_DodgeBelowBase_RaisedToBase_AndComputeReadsSkill(int skillValue, int expected)
    {
        var sheet = NewSheet(50, Skill(Dodge, skillValue));

        DerivedAttributeRules.Normalize(sheet, Catalog);

        Assert.Equal(expected, Find(sheet, Dodge).Value);
        Assert.Equal(expected, DerivedAttributeRules.Compute(sheet, Catalog).Dodge);
    }

    [Fact]
    [Trait("page", "31")]
    public void Overrides_FromBookWinOverComputed()
    {
        var sheet = NewSheet();
        sheet.Overrides.MaxHitPoints = 25;
        sheet.Overrides.DamageBonus = "+1D6";
        sheet.Overrides.Build = 2;
        sheet.Overrides.Move = 9;
        sheet.Overrides.MaxSanity = 40;

        var derived = DerivedAttributeRules.Compute(sheet, Catalog);

        Assert.Equal((25, "+1D6", 2, 9, 40),
            (derived.MaxHitPoints, derived.DamageBonus, derived.Build, derived.Move, derived.MaxSanity));
        Assert.Equal(14, derived.MaxMagicPoints); // не вписано — по формуле
    }
}
