using CampaignManager.Core.Catalogs;

namespace CampaignManager.Core.Characters;

/// <summary>Книга справочника — числа, которые снимаются в экземпляр на листе (<c>cm.books</c>).</summary>
public sealed record BookData(Guid Id, string Name)
{
    public string? Language { get; init; }
    public string? SanityLoss { get; init; }
    public int? MythosInitial { get; init; }
    public int? MythosFull { get; init; }
    public int? MythosRating { get; init; }
    public int? StudyWeeks { get; init; }
    public IReadOnlyList<string> PossibleSpells { get; init; } = [];
}

/// <summary>
/// Что Хранитель подтвердил в окне чтения. Потеря рассудка — уже число: бросили или вписали; без числа
/// окно её не применяет, иначе случайный ноль молча сберёг бы рассудок.
/// </summary>
public sealed record MythosBookReading(
    MythosBookStage Stage,
    int SanityLoss,
    bool Disbelieved,
    string? GameDate,
    IReadOnlyList<SheetSpell> SpellsToLearn,
    bool MarkLanguageSkill);

/// <summary>
/// Чтение книг Мифов (стр. 171–174, гл. 11) — единственное место этих правил: окно чтения только
/// показывает посчитанное здесь, а рассудок и Мифы меняются через <see cref="SanityRules"/>.
/// </summary>
public static class MythosBookRules
{
    /// <summary>Сколько раз удваивать срок: дальше счёт на годы теряет смысл, а int переполнится.</summary>
    private const int MaxDoublings = 16;

    private const string LanguageSkillPrefix = "Язык";

    /// <summary>Экземпляр книги из справочника: числа — копией, Хранитель вправе править их под экземпляр (стр. 222).</summary>
    public static MythosBookRecord FromCatalog(BookData book) => new()
    {
        BookId = book.Id,
        Name = book.Name,
        Language = book.Language,
        SanityLoss = book.SanityLoss,
        MythosInitial = book.MythosInitial ?? 0,
        MythosFull = book.MythosFull ?? 0,
        MythosRating = book.MythosRating ?? 0,
        StudyWeeks = book.StudyWeeks,
        PossibleSpells = [.. book.PossibleSpells],
    };

    /// <summary>Книга не из справочника (дневник культиста): числа впишет Хранитель.</summary>
    public static MythosBookRecord Custom(string name) => new() { Name = name.Trim() };

    /// <summary>Сначала начальное чтение, потом полное — и повторные полные.</summary>
    public static MythosBookStage NextStage(MythosBookRecord book) =>
        book.Stage == MythosBookStage.NotRead ? MythosBookStage.InitialReading : MythosBookStage.FullStudy;

    /// <summary>
    /// Прирост Мифов: начальное чтение — МКН; полное изучение — МКП, пока Мифы читателя ниже ЗМ книги,
    /// иначе МКН (стр. 173).
    /// </summary>
    public static int MythosGain(MythosBookRecord book, MythosBookStage stage, int currentMythos)
    {
        var gain = stage == MythosBookStage.FullStudy && currentMythos < book.MythosRating
            ? book.MythosFull
            : book.MythosInitial;

        return stage == MythosBookStage.NotRead ? 0 : Math.Max(0, gain);
    }

    /// <summary>Полное изучение даёт МКП — для подписи «почему столько».</summary>
    public static bool GetsFullGain(MythosBookRecord book, int currentMythos) => currentMythos < book.MythosRating;

    /// <summary>Срок следующего полного изучения: каждое вдвое дольше предыдущего (стр. 173). Null — срок не указан.</summary>
    public static int? NextStudyWeeks(MythosBookRecord book)
    {
        if (book.StudyWeeks is not > 0)
            return null;

        var doublings = Math.Clamp(book.FullStudyCount, 0, MaxDoublings);
        return book.StudyWeeks.Value * (1 << doublings);
    }

    /// <summary>
    /// Навык языка книги на листе — после полного изучения его отмечают (стр. 173). Ищется по вхождению
    /// языка в «Язык, …»: «латынь» находит «Язык, иностранный (латынь)»; родной язык без уточнения так не
    /// находится, его Хранитель отметит сам.
    /// </summary>
    public static SheetSkill? FindLanguageSkill(CharacterSheet sheet, SkillCatalog catalog, string? language)
    {
        if (string.IsNullOrWhiteSpace(language))
            return null;

        var name = language.Trim();
        return sheet.Skills.FirstOrDefault(s =>
        {
            var display = s.DisplayName(catalog);
            return display.StartsWith(LanguageSkillPrefix, StringComparison.OrdinalIgnoreCase)
                   && display.Contains(name, StringComparison.OrdinalIgnoreCase);
        });
    }

    /// <summary>
    /// Заклинание книги для листа: из справочника — копия, иначе запись с одним названием. Стоимость и
    /// время не придумываются — их впишет Хранитель.
    /// </summary>
    public static SheetSpell ToSheetSpell(string bookSpellName, SpellData? catalogSpell, string bookName)
    {
        if (catalogSpell is not null)
        {
            return new SheetSpell
            {
                CatalogSpellId = catalogSpell.Id,
                Name = catalogSpell.Name,
                AlternativeNames = [.. catalogSpell.AlternativeNames],
                Cost = catalogSpell.Cost ?? "",
                CastingTime = catalogSpell.CastingTime ?? "",
                Description = catalogSpell.Description,
            };
        }

        return new SheetSpell
        {
            Name = bookSpellName.Trim().Trim('«', '»', ' '),
            Description = $"Из книги «{bookName}». В каталоге заклинаний не нашлось — стоимость и время сотворения впишите по описанию Хранителя.",
        };
    }

    /// <summary>
    /// Применяет чтение: потеря рассудка одной причиной, прирост Мифов с новым максимумом Рассудка,
    /// выученные заклинания, отметка языка и стадия. Порядок важен: рассудок списывается с текущего,
    /// а уже потом новый максимум прижимает остаток.
    /// </summary>
    public static MythosBookReadingEntry Apply(
        CharacterSheet sheet, SkillCatalog catalog, MythosBookRecord book, MythosBookReading reading)
    {
        var entry = new MythosBookReadingEntry
        {
            Stage = reading.Stage,
            GameDate = string.IsNullOrWhiteSpace(reading.GameDate) ? null : reading.GameDate.Trim(),
            Disbelieved = reading.Disbelieved,
        };

        if (!reading.Disbelieved && reading.SanityLoss > 0)
            entry.SanityLost = SanityRules.ApplyLoss(sheet, catalog, reading.SanityLoss);

        var gain = MythosGain(book, reading.Stage, SanityRules.MythosValue(sheet, catalog));
        entry.MythosGained = SanityRules.AddMythos(sheet, catalog, gain);

        foreach (var spell in reading.SpellsToLearn)
        {
            if (SpellMatcher.IsKnown(sheet.Spells.Select(s => (s.Name, (IReadOnlyList<string>)s.AlternativeNames)),
                    spell.Name, spell.AlternativeNames))
                continue;

            sheet.Spells.Add(spell);
            entry.SpellsLearned.Add(spell.Name);
        }

        if (reading.Stage == MythosBookStage.FullStudy
            && reading.MarkLanguageSkill
            && FindLanguageSkill(sheet, catalog, book.Language) is { } language
            && DevelopmentPhaseRules.CanBeChecked(language, catalog))
        {
            language.Checked = true;
        }

        if (reading.Stage == MythosBookStage.FullStudy)
        {
            book.Stage = MythosBookStage.FullStudy;
            book.FullStudyCount++;
        }
        else if (book.Stage < reading.Stage)
        {
            book.Stage = reading.Stage;
        }

        book.Readings.Add(entry);
        return entry;
    }
}
