using CampaignManager.Web.Components.Features.Books.Model;
using CampaignManager.Web.Components.Features.Characters.Model;
using CampaignManager.Web.Components.Features.Characters.Services;
using CampaignManager.Web.Components.Features.Spells.Model;
using static CampaignManager.Rules.Tests.Sheet.Sheets;

namespace CampaignManager.Rules.Tests.Sheet;

/// <summary>
///     Чтение книг Мифов. Потеря рассудка приходит в правила уже числом (бросок в модалке или
///     вписанный результат), поэтому костей здесь нет.
/// </summary>
public sealed class MythosBookRulesTests
{
    private const string Latin = "Язык, иностранный (латынь)";

    [Fact]
    [Trait("page", "222")]
    public void FromCatalog_CopiesNumbers_NullsAsZero()
    {
        var book = new Book
        {
            Name = "Некрономикон", BookType = default, Language = "латынь", SanityLoss = "2d10",
            CthulhuMythosInitial = 5, CthulhuMythosFull = 15, MythosRating = 54, StudyWeeks = 68,
            PossibleSpells = ["Призыв"]
        };

        var record = MythosBookRules.FromCatalog(book);

        Assert.Equal(book.Id, record.BookId);
        Assert.Equal((5, 15, 54), (record.MythosInitial, record.MythosFull, record.MythosRating));
        Assert.Equal(68, record.StudyWeeks);
        Assert.Equal("2d10", record.SanityLoss);
        Assert.NotSame(book.PossibleSpells, record.PossibleSpells);
        Assert.Equal(MythosBookStage.NotRead, record.Stage);

        var empty = MythosBookRules.FromCatalog(new Book { Name = "Дневник", BookType = default });
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
        Assert.Equal(0, MythosBookRules.MythosGain(Book(initial: -3, full: 6, rating: 12), MythosBookStage.InitialReading, 0));

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
    public void NextStudyWeeks_DoublesEachFullStudy(int? weeks, int fullStudies, int? expected)
    {
        var book = new MythosBookRecord { StudyWeeks = weeks, FullStudyCount = fullStudies };

        Assert.Equal(expected, MythosBookRules.NextStudyWeeks(book));
    }

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
        var character = Character(50, Skill("Язык, родной", 60), Skill(Latin, 20), Skill("Латынь-клуб", 5));

        Assert.Equal(expected, MythosBookRules.FindLanguageSkill(character, language)?.Name);
    }

    [Fact]
    [Trait("page", "173")]
    public void ToCharacterSpell_FromCatalog_IsCopy()
    {
        var catalog = new Spell
        {
            Name = "Призыв", SpellType = "Призыв", Cost = "10 ПМ", CastingTime = "1 раунд",
            Description = "…", AlternativeNames = ["Вызов"]
        };

        var spell = MythosBookRules.ToCharacterSpell("«Призыв»", catalog, "Книга");

        Assert.NotEqual(catalog.Id, spell.Id);
        Assert.Equal(("Призыв", "10 ПМ", "1 раунд"), (spell.Name, spell.Cost, spell.CastingTime));
        Assert.NotSame(catalog.AlternativeNames, spell.AlternativeNames);
    }

    [Fact]
    [Trait("page", "173")]
    public void ToCharacterSpell_NotInCatalog_NameOnly()
    {
        var spell = MythosBookRules.ToCharacterSpell(" «Связь с божеством» ", null, "Книга Эйбона");

        Assert.Equal("Связь с божеством", spell.Name);
        Assert.Equal("", spell.SpellType);
        Assert.Null(spell.Cost);
        Assert.Contains("«Книга Эйбона»", spell.Description);
    }

    // ── Применение чтения ───────────────────────────────────────────────────

    [Fact]
    [Trait("page", "171-173")]
    public void Apply_InitialReading_LosesSanity_GainsInitial()
    {
        var character = WithMythos(0, 50);
        var book = Book(initial: 2, full: 6, rating: 12);

        var entry = MythosBookRules.Apply(character, book, Reading(MythosBookStage.InitialReading, 4));

        Assert.Equal(4, entry.SanityLost);
        Assert.Equal(2, entry.MythosGained);
        Assert.Equal(46, character.DerivedAttributes.Sanity.Value);
        Assert.Equal(97, character.DerivedAttributes.Sanity.MaxValue);
        Assert.Equal(4, character.State.LastSanityLoss);
        Assert.Equal(MythosBookStage.InitialReading, book.Stage);
        Assert.Equal(0, book.FullStudyCount);
        Assert.Same(entry, Assert.Single(book.Readings));
    }

    [Fact]
    [Trait("page", "177")]
    public void Apply_Disbelieved_NoSanityLoss_StillGainsMythos()
    {
        var character = WithMythos(0, 50);

        var entry = MythosBookRules.Apply(character, Book(2, 6, 12),
            Reading(MythosBookStage.InitialReading, 4, disbelieved: true));

        Assert.Equal(0, entry.SanityLost);
        Assert.Equal(2, entry.MythosGained);
        Assert.True(entry.Disbelieved);
        Assert.Equal(50, character.DerivedAttributes.Sanity.Value);
    }

    [Fact]
    [Trait("page", "173")]
    public void Apply_FullStudy_GainsFull_MarksLanguage_CountsStudies()
    {
        var character = WithMythos(5, 50, Skill(Latin, 20));
        var book = Book(2, 6, 12);
        book.Language = "латынь";

        MythosBookRules.Apply(character, book, Reading(MythosBookStage.FullStudy, 0, markLanguage: true));
        var second = MythosBookRules.Apply(character, book, Reading(MythosBookStage.FullStudy, 0));

        Assert.True(Find(character, Latin).IsUsed);
        Assert.Equal(MythosBookStage.FullStudy, book.Stage);
        Assert.Equal(2, book.FullStudyCount);
        Assert.Equal(6, second.MythosGained); // 11 всё ещё ниже ЗМ 12
        Assert.Equal(17, SanityRules.GetMythosValue(character));
        Assert.Equal(40, MythosBookRules.NextStudyWeeks(book));
    }

    [Fact]
    [Trait("page", "173")]
    public void Apply_FullStudyWithoutMarkFlag_LanguageUntouched()
    {
        var character = WithMythos(0, 50, Skill(Latin, 20));
        var book = Book(2, 6, 12);
        book.Language = "латынь";

        MythosBookRules.Apply(character, book, Reading(MythosBookStage.FullStudy, 0));

        Assert.False(Find(character, Latin).IsUsed);
    }

    [Fact]
    [Trait("page", "171-173")]
    public void Apply_InitialAfterFull_StageStaysFull()
    {
        var book = Book(2, 6, 12);
        book.Stage = MythosBookStage.FullStudy;
        book.FullStudyCount = 1;

        MythosBookRules.Apply(WithMythos(0), book, Reading(MythosBookStage.InitialReading, 0));

        Assert.Equal(MythosBookStage.FullStudy, book.Stage);
        Assert.Equal(1, book.FullStudyCount);
    }

    /// <summary>Сначала потеря с текущего Рассудка, потом новый максимум прижимает остаток.</summary>
    [Fact]
    [Trait("page", "171-173")]
    public void Apply_LossBeforeMythos_ThenClampedToNewMax()
    {
        var character = WithMythos(0, 98);

        var entry = MythosBookRules.Apply(character, Book(2, 6, 12), Reading(MythosBookStage.FullStudy, 1));

        Assert.Equal(1, entry.SanityLost);
        Assert.Equal(93, character.DerivedAttributes.Sanity.Value);
    }

    [Fact]
    [Trait("page", "173")]
    public void Apply_NoMythosSkill_NothingGained()
    {
        var entry = MythosBookRules.Apply(Character(50), Book(2, 6, 12), Reading(MythosBookStage.InitialReading, 0));

        Assert.Equal(0, entry.MythosGained);
    }

    [Fact]
    [Trait("page", "173")]
    public void Apply_Spells_SkipsAlreadyKnown()
    {
        var character = WithMythos(0, 50);
        character.Spells.Add(new Spell { Name = "Призыв", SpellType = "" });

        var entry = MythosBookRules.Apply(character, Book(2, 6, 12), Reading(MythosBookStage.InitialReading, 0,
            spells: [new Spell { Name = "призыв", SpellType = "" }, new Spell { Name = "Связь", SpellType = "" }]));

        Assert.Equal(new[] { "Связь" }, entry.SpellsLearned);
        Assert.Equal(2, character.Spells.Count);
    }

    [Theory]
    [Trait("page", "171-173")]
    [InlineData("  март 1925 ", "март 1925")]
    [InlineData("   ", null)]
    [InlineData(null, null)]
    public void Apply_GameDate_TrimmedOrNull(string? date, string? expected)
    {
        var entry = MythosBookRules.Apply(WithMythos(0), Book(2, 6, 12),
            Reading(MythosBookStage.InitialReading, 0) with { GameDate = date });

        Assert.Equal(expected, entry.GameDate);
    }

    [Fact]
    [Trait("page", "156")]
    public void Apply_InsaneReader_AnyLossMakesBoutDue()
    {
        var character = WithMythos(0, 50);
        character.State.HasTemporaryInsanity = true;

        MythosBookRules.Apply(character, Book(2, 6, 12), Reading(MythosBookStage.InitialReading, 1));

        Assert.True(character.State.InsanityBoutDue);
    }

    private static MythosBookRecord Book(int initial, int full, int rating) => new()
    {
        Name = "Книга", MythosInitial = initial, MythosFull = full, MythosRating = rating, StudyWeeks = 10
    };

    private static MythosBookReading Reading(
        MythosBookStage stage, int sanityLoss, bool disbelieved = false, bool markLanguage = false,
        IReadOnlyList<Spell>? spells = null) =>
        new(stage, sanityLoss, disbelieved, null, spells ?? [], markLanguage);
}
