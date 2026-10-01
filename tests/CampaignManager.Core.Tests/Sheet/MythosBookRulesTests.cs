using CampaignManager.Core.Catalogs;
using CampaignManager.Core.Characters;
using static CampaignManager.Core.Tests.Sheet.Sheets;

namespace CampaignManager.Core.Tests.Sheet;

/// <summary>
/// Чтение книг Мифов. Потеря рассудка приходит в правила уже числом, поэтому костей здесь нет.
/// Перенесено из T0.2 без правки ожиданий, кроме <c>Apply_NoMythosRow…</c> (следствие F-S05).
/// </summary>
public sealed class MythosBookRulesTests
{
    [Fact]
    [Trait("page", "222")]
    public void FromCatalog_CopiesNumbers_NullsAsZero()
    {
        var book = new BookData(Guid.NewGuid(), "Некрономикон")
        {
            Language = "латынь", SanityLoss = "2d10", MythosInitial = 5, MythosFull = 15, MythosRating = 54,
            StudyWeeks = 68, PossibleSpells = ["Призыв"],
        };

        var record = MythosBookRules.FromCatalog(book);

        Assert.Equal(book.Id, record.BookId);
        Assert.Equal((5, 15, 54), (record.MythosInitial, record.MythosFull, record.MythosRating));
        Assert.Equal(68, record.StudyWeeks);
        Assert.Equal("2d10", record.SanityLoss);
        Assert.NotSame(book.PossibleSpells, record.PossibleSpells);
        Assert.Equal(MythosBookStage.NotRead, record.Stage);

        var empty = MythosBookRules.FromCatalog(new BookData(Guid.NewGuid(), "Дневник"));
        Assert.Equal((0, 0, 0), (empty.MythosInitial, empty.MythosFull, empty.MythosRating));
        Assert.Null(empty.StudyWeeks);
    }

    [Fact]
    [Trait("page", "222")]
    public void Custom_TrimsName_NoCatalogLink()
    {
        var record = MythosBookRules.Custom("  Дневник культиста ");

        Assert.Equal("Дневник культиста", record.Name);
        Assert.Null(record.BookId);
    }

    [Theory]
    [Trait("page", "171-173")]
    [InlineData(MythosBookStage.NotRead, MythosBookStage.InitialReading)]
    [InlineData(MythosBookStage.InitialReading, MythosBookStage.FullStudy)]
    [InlineData(MythosBookStage.FullStudy, MythosBookStage.FullStudy)]
    public void NextStage_InitialThenFullForever(MythosBookStage stage, MythosBookStage expected) =>
        Assert.Equal(expected, MythosBookRules.NextStage(new MythosBookRecord { Stage = stage }));

    [Theory]
    [Trait("page", "173")]
    [InlineData(MythosBookStage.NotRead, 0, 0)]
    [InlineData(MythosBookStage.InitialReading, 0, 2)]
    [InlineData(MythosBookStage.InitialReading, 30, 2)]
    [InlineData(MythosBookStage.FullStudy, 11, 6)] // ниже ЗМ 12 — МКП
    [InlineData(MythosBookStage.FullStudy, 12, 2)] // достиг ЗМ — снова МКН
    [InlineData(MythosBookStage.FullStudy, 40, 2)]
    public void MythosGain_FullOnlyBelowRating(MythosBookStage stage, int currentMythos, int expected)
    {
        var book = Book(initial: 2, full: 6, rating: 12);

        Assert.Equal(expected, MythosBookRules.MythosGain(book, stage, currentMythos));
        Assert.Equal(currentMythos < 12, MythosBookRules.GetsFullGain(book, currentMythos));
    }

    [Fact]
    [Trait("page", "173")]
    public void MythosGain_NegativeNumbers_Zero() =>
        Assert.Equal(0, MythosBookRules.MythosGain(Book(-3, 6, 12), MythosBookStage.InitialReading, 0));

    [Theory]
    [Trait("page", "173")]
    [InlineData(null, 0, null)]
    [InlineData(0, 0, null)]
    [InlineData(-4, 0, null)]
    [InlineData(10, 0, 10)]
    [InlineData(10, 1, 20)]
    [InlineData(10, 3, 80)]
    [InlineData(10, -2, 10)]
    [InlineData(10, 20, 655360)] // удвоений не больше 16
    public void NextStudyWeeks_DoublesEachFullStudy(int? weeks, int fullStudies, int? expected) =>
        Assert.Equal(expected, MythosBookRules.NextStudyWeeks(new MythosBookRecord { StudyWeeks = weeks, FullStudyCount = fullStudies }));

    [Theory]
    [Trait("page", "173")]
    [InlineData("латынь", Latin)]
    [InlineData(" Латынь ", Latin)]
    [InlineData("греческий", null)]
    [InlineData("английский", null)] // родной язык без уточнения не находится
    [InlineData("", null)]
    [InlineData(null, null)]
    public void FindLanguageSkill_ByLanguageInsideLanguageSkillName(string? language, string? expected)
    {
        var sheet = NewSheet(50, Skill(OwnLanguage, 60), Skill(Latin, 20), new SheetSkill { Name = "Латынь-клуб", Value = 5 });

        Assert.Equal(expected, MythosBookRules.FindLanguageSkill(sheet, Catalog, language)?.DisplayName(Catalog));
    }

    [Fact]
    [Trait("page", "173")]
    public void FindLanguageSkill_OwnSpecializationOutsideCatalog()
    {
        var sheet = NewSheet(50, Specialization(ForeignLanguage, "арамейский", 10));

        Assert.Equal("Язык, иностранный (арамейский)",
            MythosBookRules.FindLanguageSkill(sheet, Catalog, "арамейский")?.DisplayName(Catalog));
    }

    [Fact]
    [Trait("page", "173")]
    public void ToSheetSpell_FromCatalog_IsCopy()
    {
        var catalog = new SpellData(Guid.NewGuid(), "Призыв")
        {
            Cost = "10 ПМ", CastingTime = "1 раунд", Description = "…", AlternativeNames = ["Вызов"],
        };

        var spell = MythosBookRules.ToSheetSpell("«Призыв»", catalog, "Книга");

        Assert.Equal(catalog.Id, spell.CatalogSpellId);
        Assert.Equal(("Призыв", "10 ПМ", "1 раунд"), (spell.Name, spell.Cost, spell.CastingTime));
        Assert.NotSame(catalog.AlternativeNames, spell.AlternativeNames);
        Assert.Equal(["Вызов"], spell.AlternativeNames);
    }

    [Fact]
    [Trait("page", "173")]
    public void ToSheetSpell_NotInCatalog_NameOnly()
    {
        var spell = MythosBookRules.ToSheetSpell(" «Связь с божеством» ", null, "Книга Эйбона");

        Assert.Equal("Связь с божеством", spell.Name);
        Assert.Null(spell.CatalogSpellId);
        Assert.Equal("", spell.Cost);
        Assert.Contains("«Книга Эйбона»", spell.Description);
    }

    // ── Применение чтения ───────────────────────────────────────────────────

    [Fact]
    [Trait("page", "171-173")]
    public void Apply_InitialReading_LosesSanity_GainsInitial()
    {
        var sheet = WithMythos(0, 50);
        var book = Book(2, 6, 12);

        var entry = MythosBookRules.Apply(sheet, Catalog, book, Reading(MythosBookStage.InitialReading, 4));

        Assert.Equal(4, entry.SanityLost);
        Assert.Equal(2, entry.MythosGained);
        Assert.Equal(46, sheet.Current.Sanity);
        Assert.Equal(97, SanityRules.ComputeMaxSanity(sheet, Catalog));
        Assert.Equal(4, sheet.Condition.LastSanityLoss);
        Assert.Equal(MythosBookStage.InitialReading, book.Stage);
        Assert.Equal(0, book.FullStudyCount);
        Assert.Same(entry, Assert.Single(book.Readings));
    }

    [Fact]
    [Trait("page", "177")]
    public void Apply_Disbelieved_NoSanityLoss_StillGainsMythos()
    {
        var sheet = WithMythos(0, 50);

        var entry = MythosBookRules.Apply(sheet, Catalog, Book(2, 6, 12),
            Reading(MythosBookStage.InitialReading, 4, disbelieved: true));

        Assert.Equal(0, entry.SanityLost);
        Assert.Equal(2, entry.MythosGained);
        Assert.True(entry.Disbelieved);
        Assert.Equal(50, sheet.Current.Sanity);
    }

    [Fact]
    [Trait("page", "173")]
    public void Apply_FullStudy_GainsFull_MarksLanguage_CountsStudies()
    {
        var sheet = WithMythos(5, 50, Skill(Latin, 20));
        var book = Book(2, 6, 12);
        book.Language = "латынь";

        MythosBookRules.Apply(sheet, Catalog, book, Reading(MythosBookStage.FullStudy, 0, markLanguage: true));
        var second = MythosBookRules.Apply(sheet, Catalog, book, Reading(MythosBookStage.FullStudy, 0));

        Assert.True(Find(sheet, Latin).Checked);
        Assert.Equal(MythosBookStage.FullStudy, book.Stage);
        Assert.Equal(2, book.FullStudyCount);
        Assert.Equal(6, second.MythosGained); // 11 всё ещё ниже ЗМ 12
        Assert.Equal(17, SanityRules.MythosValue(sheet, Catalog));
        Assert.Equal(40, MythosBookRules.NextStudyWeeks(book));
    }

    [Fact]
    [Trait("page", "173")]
    public void Apply_FullStudyWithoutMarkFlag_LanguageUntouched()
    {
        var sheet = WithMythos(0, 50, Skill(Latin, 20));
        var book = Book(2, 6, 12);
        book.Language = "латынь";

        MythosBookRules.Apply(sheet, Catalog, book, Reading(MythosBookStage.FullStudy, 0));

        Assert.False(Find(sheet, Latin).Checked);
    }

    [Fact]
    [Trait("page", "171-173")]
    public void Apply_InitialAfterFull_StageStaysFull()
    {
        var book = Book(2, 6, 12);
        book.Stage = MythosBookStage.FullStudy;
        book.FullStudyCount = 1;

        MythosBookRules.Apply(WithMythos(0), Catalog, book, Reading(MythosBookStage.InitialReading, 0));

        Assert.Equal(MythosBookStage.FullStudy, book.Stage);
        Assert.Equal(1, book.FullStudyCount);
    }

    /// <summary>Сначала потеря с текущего Рассудка, потом новый максимум прижимает остаток.</summary>
    [Fact]
    [Trait("page", "171-173")]
    public void Apply_LossBeforeMythos_ThenClampedToNewMax()
    {
        var sheet = WithMythos(0, 98);

        var entry = MythosBookRules.Apply(sheet, Catalog, Book(2, 6, 12), Reading(MythosBookStage.FullStudy, 1));

        Assert.Equal(1, entry.SanityLost);
        Assert.Equal(93, sheet.Current.Sanity);
    }

    /// <summary>
    /// В v1 без строки Мифов чтение ничего не прибавляло. Мифы 2.0 берутся из справочника — строка
    /// заводится; без Мифов в справочнике прибавлять по-прежнему некуда (следствие F-S05).
    /// </summary>
    [Fact]
    [Trait("page", "173")]
    [Trait("finding", "F-S05")]
    public void Apply_NoMythosRow_RowCreated_NoMythosInCatalog_NothingGained()
    {
        var entry = MythosBookRules.Apply(NewSheet(50), Catalog, Book(2, 6, 12), Reading(MythosBookStage.InitialReading, 0));
        Assert.Equal(2, entry.MythosGained);

        var bare = MythosBookRules.Apply(NewSheet(50), CatalogWithoutMythosAndCredit, Book(2, 6, 12),
            Reading(MythosBookStage.InitialReading, 0));
        Assert.Equal(0, bare.MythosGained);
    }

    [Fact]
    [Trait("page", "173")]
    public void Apply_Spells_SkipsAlreadyKnown()
    {
        var sheet = WithMythos(0, 50);
        sheet.Spells.Add(new SheetSpell { Name = "Призыв" });

        var entry = MythosBookRules.Apply(sheet, Catalog, Book(2, 6, 12), Reading(MythosBookStage.InitialReading, 0,
            spells: [new SheetSpell { Name = "призыв" }, new SheetSpell { Name = "Связь" }]));

        Assert.Equal(["Связь"], entry.SpellsLearned);
        Assert.Equal(2, sheet.Spells.Count);
    }

    [Fact]
    [Trait("page", "173")]
    public void Apply_Spells_KnownByAlternativeName()
    {
        var sheet = WithMythos(0, 50);
        sheet.Spells.Add(new SheetSpell { Name = "Призыв", AlternativeNames = ["Вызов"] });

        var entry = MythosBookRules.Apply(sheet, Catalog, Book(2, 6, 12), Reading(MythosBookStage.InitialReading, 0,
            spells: [new SheetSpell { Name = "«Вызов»" }]));

        Assert.Empty(entry.SpellsLearned);
    }

    [Theory]
    [Trait("page", "171-173")]
    [InlineData("  март 1925 ", "март 1925")]
    [InlineData("   ", null)]
    [InlineData(null, null)]
    public void Apply_GameDate_TrimmedOrNull(string? date, string? expected)
    {
        var entry = MythosBookRules.Apply(WithMythos(0), Catalog, Book(2, 6, 12),
            Reading(MythosBookStage.InitialReading, 0) with { GameDate = date });

        Assert.Equal(expected, entry.GameDate);
    }

    [Fact]
    [Trait("page", "156")]
    public void Apply_InsaneReader_AnyLossMakesBoutDue()
    {
        var sheet = WithMythos(0, 50);
        sheet.Condition.TemporaryInsanity = true;

        MythosBookRules.Apply(sheet, Catalog, Book(2, 6, 12), Reading(MythosBookStage.InitialReading, 1));

        Assert.True(sheet.Condition.BoutDue);
    }

    [Fact]
    public void SpellMatcher_ExactAfterNormalization_QuotedAndBeforeBracket()
    {
        SpellData[] catalog =
        [
            new(Guid.NewGuid(), "Связь с бесформенным отродьем"),
            new(Guid.NewGuid(), "Призыв") { AlternativeNames = ["Вызов"] },
        ];

        Assert.Equal("Связь с бесформенным отродьем",
            SpellMatcher.Match(catalog, "Связь с бесформенным отродьем Жотакуа («Связь с бесформенным отродьем»)")?.Name);
        Assert.Equal("Призыв", SpellMatcher.Match(catalog, "  вызов. ")?.Name);
        Assert.Null(SpellMatcher.Match(catalog, "Связь с божеством: Кфулхут"));
    }

    private static MythosBookRecord Book(int initial, int full, int rating) => new()
    {
        Name = "Книга", MythosInitial = initial, MythosFull = full, MythosRating = rating, StudyWeeks = 10,
    };

    private static MythosBookReading Reading(
        MythosBookStage stage, int sanityLoss, bool disbelieved = false, bool markLanguage = false,
        IReadOnlyList<SheetSpell>? spells = null) =>
        new(stage, sanityLoss, disbelieved, null, spells ?? [], markLanguage);
}
