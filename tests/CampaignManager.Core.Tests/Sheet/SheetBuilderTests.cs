using CampaignManager.Core.Catalogs;
using CampaignManager.Core.Characters;
using CampaignManager.Core.Dice;
using static CampaignManager.Core.Tests.Sheet.Sheets;

namespace CampaignManager.Core.Tests.Sheet;

/// <summary>
/// Один конструктор листа (T2.4): помощник, быстрый НПС, импорт сценария и чистый лист дают листы одного
/// устройства — те же имена навыков (родной язык один, «Язык, родной», а не «Языки (родной)» у части сборщиков,
/// как в v1) и те же производные по одним характеристикам.
/// </summary>
public sealed class SheetBuilderTests
{
    private const int Age = 25;

    private static readonly Characteristics Same = Chars(str: 55, con: 60, siz: 65, dex: 70, app: 45, @int: 75, pow: 60, edu: 80);

    private static CharacterSheet FromDraft()
    {
        var draft = new InvestigatorDraft { Age = Age };
        foreach (var key in Enum.GetValues<Characteristic>())
            draft.SetCharacteristic(key, Same[key]);
        return SheetBuilder.FromDraft(draft, Catalog, occupation: null);
    }

    private static CharacterSheet QuickNpc() =>
        SheetBuilder.QuickNpc(new QuickNpcDraft { Name = "Констебль", Age = Age, Characteristics = Same with { } }, Catalog);

    private static CharacterSheet Imported() =>
        SheetBuilder.FromImport(new ImportedCharacter { Name = "Профессор", Age = Age, Characteristics = Same with { } }, Catalog).Sheet;

    private static CharacterSheet Blank()
    {
        var sheet = SheetBuilder.Blank(Catalog);
        sheet.Characteristics = Same with { };
        sheet.Personal.Age = Age;
        DerivedAttributeRules.InitializeNewSheet(sheet, Catalog);
        return sheet;
    }

    public static TheoryData<string> Modes => ["draft", "quick", "import", "blank"];

    private static CharacterSheet Build(string mode) => mode switch
    {
        "draft" => FromDraft(),
        "quick" => QuickNpc(),
        "import" => Imported(),
        _ => Blank(),
    };

    private static List<(string Name, int Value)> Lines(CharacterSheet sheet) =>
    [
        .. SheetSkillLayout.Groups(sheet, Catalog)
            .SelectMany(g => g.Lines.Concat(g.Folds.SelectMany(f => f.Lines)))
            .Select(l => (l.Name, l.Value))
            .OrderBy(l => l.Name, StringComparer.Ordinal),
    ];

    [Fact]
    public void AllModes_SameSkillNamesAndValues()
    {
        var reference = Lines(FromDraft());
        foreach (var mode in Modes)
            Assert.Equal(reference, Lines(Build(mode!)));
    }

    [Fact]
    public void AllModes_SameDerivedAttributes()
    {
        var reference = DerivedAttributeRules.Compute(FromDraft(), Catalog);
        foreach (var mode in Modes)
            Assert.Equal(reference, DerivedAttributeRules.Compute(Build(mode!), Catalog));
    }

    /// <summary>Родной язык = ОБР, Уклонение = ½ ЛВК во всех режимах — формулой справочника, а не копией числа (стр. 57, 77).</summary>
    [Theory]
    [Trait("page", "57, 77")]
    [MemberData(nameof(Modes))]
    public void AllModes_OwnLanguageIsEdu_DodgeIsHalfDex_WithoutRows(string mode)
    {
        var sheet = Build(mode);

        Assert.Equal(80, sheet.Value(Catalog, SkillCodes.LanguageOwn));
        Assert.Equal(35, sheet.Value(Catalog, SkillCodes.Dodge));
        Assert.Single(Lines(sheet), l => l.Name == OwnLanguage);
        // Строки нет: правка ОБР на листе сама поднимет родной язык.
        Assert.Null(sheet.Entry(Catalog, SkillCodes.LanguageOwn));
    }

    [Theory]
    [Trait("page", "31")]
    [MemberData(nameof(Modes))]
    public void AllModes_NewSheet_CurrentAtMaximum_SanityIsPow(string mode)
    {
        var sheet = Build(mode);
        var derived = DerivedAttributeRules.Compute(sheet, Catalog);

        Assert.Equal(derived.MaxHitPoints, sheet.Current.HitPoints);
        Assert.Equal(derived.MaxMagicPoints, sheet.Current.MagicPoints);
        Assert.Equal(60, sheet.Current.Sanity);
        Assert.Equal(12, derived.MaxHitPoints);
    }

    // ── Помощник ─────────────────────────────────────────────────────────────

    private static OccupationDefinition Doctor() => new(Guid.NewGuid(), "Врач", SkillPointsFormula.Edu4)
    {
        CreditRatingMin = 30,
        CreditRatingMax = 80,
        Slots =
        [
            new OccupationSlotDefinition(OccupationSlotKind.Skill) { SkillId = Id("Психология") },
            new OccupationSlotDefinition(OccupationSlotKind.Specialization) { SkillId = Id(ForeignLanguage), Specialization = "латынь" },
            new OccupationSlotDefinition(OccupationSlotKind.Specialization) { SkillId = Id(Science), Specialization = "биология" },
            new OccupationSlotDefinition(OccupationSlotKind.Free),
        ],
    };

    [Fact]
    [Trait("page", "34, 44-45")]
    public void FromDraft_PointsCreditFinancesAndNamedSpecialization()
    {
        var occupation = Doctor();
        var draft = new InvestigatorDraft { Age = Age, Era = Era.Classic };
        foreach (var key in Enum.GetValues<Characteristic>())
            draft.SetCharacteristic(key, Same[key]);
        draft.SetLuckRolls([55]);
        CreationPlan.SelectOccupation(draft, occupation);
        var plan = new CreationPlan(draft, Catalog, occupation);
        plan.SyncSlots();
        plan.SetChoice(3, CreationPlan.KeyOf(Id("Слух")));
        plan.SetOccupationPoints(CreationPlan.KeyOf(Id("Психология")), 50);
        plan.SetOccupationPoints("биология", 40);
        plan.SetPersonalPoints(CreationPlan.KeyOf(Id("Слух")), 10);
        plan.SetCreditRating(40);
        draft.Personal.Name = "  Элеонора Грей ";
        draft.Biography.Traits = "Верная";
        draft.KeyConnectionSection = "traits";

        var sheet = SheetBuilder.FromDraft(plan);

        Assert.Equal("Элеонора Грей", sheet.Personal.Name);
        Assert.Equal("Врач", sheet.Personal.Occupation);
        Assert.Equal(occupation.Id, sheet.Personal.OccupationId);
        Assert.Equal(60, Find(sheet, "Психология").Value);
        Assert.Equal(41, Find(sheet, "Наука (биология)").Value); // своя специализация: база соседней (1) + 40
        Assert.Contains(Lines(sheet), l => l.Name == Latin && l.Value == 1); // латынь есть в справочнике — на базе без строки
        Assert.Equal(30, Find(sheet, "Слух").Value);
        Assert.Equal(40, Find(sheet, CreditRating).Value);
        Assert.Equal(55, sheet.Current.Luck);
        Assert.Equal(80m, sheet.Finances.Cash); // Средства 40, 1920-е: × 2
        Assert.Equal("Верная", sheet.Biography.KeyConnection);
    }

    // ── Быстрый НПС ──────────────────────────────────────────────────────────

    [Fact]
    [Trait("page", "187")]
    public void QuickNpc_Archetype_SkillsByCode_BrawlAndHandgunAtCombatLevel()
    {
        var draft = new QuickNpcDraft { Name = "Сержант" };
        QuickNpcRules.ApplyArchetype(draft, QuickNpcRules.Archetypes.Single(a => a.Key == "police"), Catalog);
        draft.Note = "Знает всех в порту.";

        var sheet = SheetBuilder.QuickNpc(draft, Catalog);

        Assert.Equal("Полицейский", sheet.Personal.Occupation);
        Assert.Equal(50, Find(sheet, "Внимание").Value);
        Assert.Equal(50, Find(sheet, "Запугивание").Value);
        Assert.Equal(40, Find(sheet, "Ближний бой (драка)").Value);
        Assert.Equal(40, Find(sheet, "Стрельба (пистолет)").Value);
        Assert.Equal("Знает всех в порту.", sheet.Biography.Backstory);
    }

    [Fact]
    public void QuickNpc_Weapon_CopiedWithSkillAtCombatLevel_ReplacedOnChange()
    {
        var draft = new QuickNpcDraft { Name = "Громила" };
        QuickNpcRules.ApplyCombatLevel(draft, QuickNpcCombatLevel.Professional, Catalog);
        var rifle = new WeaponData(Guid.NewGuid(), "Винтовка") { SkillId = Id("Стрельба (винтовка)"), Damage = "2d6+4" };
        QuickNpcRules.ApplyWeapon(draft, rifle, Catalog);
        Assert.Contains(draft.Skills, r => r.SkillId == Id("Стрельба (винтовка)") && r.Value == 70);

        var shotgun = new WeaponData(Guid.NewGuid(), "Дробовик") { SkillId = Id("Стрельба (дробовик)") };
        QuickNpcRules.ApplyWeapon(draft, shotgun, Catalog);

        Assert.DoesNotContain(draft.Skills, r => r.SkillId == Id("Стрельба (винтовка)"));
        var sheet = SheetBuilder.QuickNpc(draft, Catalog);
        Assert.Equal("Дробовик", Assert.Single(sheet.Weapons).Name);
        Assert.Equal(70, Find(sheet, "Стрельба (дробовик)").Value);
        Assert.Equal(70, Find(sheet, "Ближний бой (драка)").Value);
    }

    // ── Импорт ───────────────────────────────────────────────────────────────

    [Fact]
    public void FromImport_SkillNames_OldSpellingsSpecializationsAndOwn()
    {
        var data = new ImportedCharacter
        {
            Name = "Профессор",
            Characteristics = Same with { },
            Skills = new Dictionary<string, int>
            {
                ["Языки (родной)"] = 90,
                ["Язык (латынь)"] = 40,
                ["Наука (астрономия)"] = 30,
                ["внимание"] = 60,
                ["Хиромантия"] = 25,
            },
        };

        var (sheet, own) = SheetBuilder.FromImport(data, Catalog);

        Assert.Equal(90, sheet.Value(Catalog, SkillCodes.LanguageOwn));
        Assert.Equal(40, Find(sheet, Latin).Value);
        Assert.Equal(30, Find(sheet, "Наука (астрономия)").Value);
        Assert.Equal(60, Find(sheet, "Внимание").Value);
        Assert.Equal(25, Find(sheet, "Хиромантия").Value);
        Assert.Equal(["Хиромантия"], own);
        Assert.DoesNotContain(sheet.Skills, s => s.Checked); // в v1 импорт ставил отметку развития всем навыкам
    }

    [Fact]
    public void ToImport_IsFixedPointOfFromImport_AndSkipsSkillsAtBase()
    {
        // Лист, как его ведут на деле (T2.5d, экспорт сценария): строка на базе, специализация вне справочника, свой
        // навык, Уклонение строкой, поправка ПЗ из книги, оружие, заклинание, графы биографии.
        var sheet = new CharacterSheet
        {
            Personal = new PersonalInfo { Name = "Профессор", Occupation = "Профессор", Age = 52, Birthplace = "Бостон" },
            Characteristics = Same with { },
            Current = new CurrentValues { HitPoints = 3, MagicPoints = 12, Sanity = 41, Luck = 45 },
            Overrides = new SheetOverrides { MaxHitPoints = 20 },
            Skills =
            [
                Skill("Внимание", 65),
                Skill("Слух", Def("Слух").BaseValue),
                Skill("Уклонение", 50),
                Specialization("Наука", "геология", 30),
                new SheetSkill { Name = "Хиромантия", Value = 20 },
            ],
            Weapons = [new SheetWeapon { Name = "Револьвер", SkillId = Id("Стрельба (пистолет)"), Damage = "1d10", Ammo = "6" }],
            Spells = [new SheetSpell { Name = "Знак Воорта", Cost = "5 ПМ" }],
            Biography = new Biography { Backstory = "Ведёт раскопки", Appearance = "Седой" },
        };

        var exported = SheetBuilder.ToImport(sheet, Catalog);
        var again = SheetBuilder.ToImport(SheetBuilder.FromImport(exported, Catalog).Sheet, Catalog);

        Assert.DoesNotContain("Слух", exported.Skills.Keys); // на базе — пустая графа бланка
        Assert.DoesNotContain("Уклонение", exported.Skills.Keys); // Уклонение — полем
        Assert.Equal((20, 50), (exported.HitPoints, exported.Dodge));
        Assert.Equal(exported.Skills.OrderBy(p => p.Key, StringComparer.Ordinal), again.Skills.OrderBy(p => p.Key, StringComparer.Ordinal));
        Assert.Equal((exported.HitPoints, exported.MagicPoints, exported.Sanity, exported.Luck, exported.DamageBonus, exported.Build, exported.MoveSpeed, exported.Dodge),
            (again.HitPoints, again.MagicPoints, again.Sanity, again.Luck, again.DamageBonus, again.Build, again.MoveSpeed, again.Dodge));
        Assert.Equal(exported.Biography, again.Biography);
        Assert.Equal("Стрельба (пистолет)", Assert.Single(again.Weapons).Skill);
        Assert.Equal("Знак Воорта", Assert.Single(again.Spells).Name);
        Assert.Equal("Бостон", again.Birthplace);
    }

    [Fact]
    public void FromImport_PrintedValues_OverridesOnlyWhereFormulaDiffers()
    {
        var c = Same with { };
        var (build, bonus) = DerivedAttributeRules.ComputeBuildAndDamageBonus(c);
        var data = new ImportedCharacter
        {
            Name = "Культист",
            Age = Age,
            Characteristics = c,
            HitPoints = 15, // по формуле 12
            MagicPoints = DerivedAttributeRules.ComputeMaxMagicPoints(c),
            DamageBonus = bonus.Replace("D", "д", StringComparison.Ordinal),
            Build = build.ToString(System.Globalization.CultureInfo.InvariantCulture),
            MoveSpeed = 8,
            Dodge = 50,
            Sanity = 45,
            Luck = 40,
        };

        var sheet = SheetBuilder.FromImport(data, Catalog).Sheet;

        Assert.Equal(15, sheet.Overrides.MaxHitPoints);
        Assert.Equal(15, sheet.Current.HitPoints);
        Assert.Null(sheet.Overrides.MaxMagicPoints);
        Assert.Null(sheet.Overrides.DamageBonus);
        Assert.Null(sheet.Overrides.Build);
        Assert.Null(sheet.Overrides.Move);
        Assert.Equal(50, sheet.Value(Catalog, SkillCodes.Dodge));
        Assert.Equal(45, sheet.Current.Sanity);
        Assert.Equal(40, sheet.Current.Luck);
    }

    // ── Случайный сыщик ──────────────────────────────────────────────────────

    [Theory]
    [InlineData(1)]
    [InlineData(7)]
    [InlineData(42)]
    [InlineData(2026)]
    public void RandomDraft_PassesEveryStep_OpensOnSummary(int seed)
    {
        var occupation = Doctor();
        var draft = SheetBuilder.RandomDraft(Catalog, [occupation], Era.Classic, new SeededDiceRoller(seed));
        var plan = new CreationPlan(draft, Catalog, occupation);

        Assert.Null(plan.FirstInvalidStep());
        Assert.Equal((int)CreationStep.Summary, draft.StepIndex);
        Assert.InRange(draft.CreditRating, 30, 80);
        Assert.True(draft.SpentOccupationPoints <= plan.OccupationBudget);
        Assert.True(draft.SpentPersonalPoints <= plan.PersonalBudget);
        Assert.False(string.IsNullOrWhiteSpace(draft.Personal.Name));
        Assert.NotEqual("", draft.KeyConnectionSection);

        var sheet = SheetBuilder.FromDraft(plan);
        Assert.Subset(Lines(sheet).Select(l => l.Name).ToHashSet(), Lines(Blank()).Select(l => l.Name).ToHashSet());
        Assert.Equal(draft.Luck, sheet.Current.Luck);
    }

    [Fact]
    public void RandomDraft_SameSeed_SameDraft()
    {
        var a = SheetBuilder.RandomDraft(Catalog, [Doctor()], Era.Classic, new SeededDiceRoller(5));
        var b = SheetBuilder.RandomDraft(Catalog, [Doctor()], Era.Classic, new SeededDiceRoller(5));

        Assert.Equal(a.Personal.Name, b.Personal.Name);
        Assert.Equal(a.Rolled, b.Rolled);
    }
}
