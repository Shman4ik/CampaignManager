using CampaignManager.Web.Components.Features.Books.Model;
using CampaignManager.Web.Components.Features.Characters.Model;
using CampaignManager.Web.Components.Features.Spells.Model;
using CampaignManager.Web.Components.Features.Spells.Services;

namespace CampaignManager.Web.Components.Features.Characters.Services;

/// <summary>
///     Чтение книг Мифов (гл. 9 «Магия», стр. 171–174; гл. 11 «Книги мистических знаний»).
///     Единственное место, где живут эти правила: модалка чтения только показывает то, что
///     посчитано здесь, а рассудок и Мифы меняет через <see cref="SanityRules" />.
/// </summary>
public static class MythosBookRules
{
    /// <summary>Сколько раз удваивать срок — дальше счёт на годы теряет смысл, а int переполнится.</summary>
    private const int MaxDoublings = 16;

    private const string LanguageSkillPrefix = "Язык";

    /// <summary>
    ///     Экземпляр книги из каталога. Числа снимаются копией: Хранитель вправе поправить их
    ///     под конкретный экземпляр, не трогая справочник (стр. 222).
    /// </summary>
    public static MythosBookRecord FromCatalog(Book book) => new()
    {
        BookId = book.Id,
        Name = book.Name,
        Language = book.Language,
        SanityLoss = book.SanityLoss,
        MythosInitial = book.CthulhuMythosInitial ?? 0,
        MythosFull = book.CthulhuMythosFull ?? 0,
        MythosRating = book.MythosRating ?? 0,
        StudyWeeks = book.StudyWeeks,
        PossibleSpells = book.PossibleSpells.ToList()
    };

    /// <summary>Книга, которой нет в каталоге (дневник культиста из сценария): числа впишет Хранитель.</summary>
    public static MythosBookRecord Custom(string name) => new() { Name = name.Trim() };

    /// <summary>Следующий шаг чтения: сначала начальное, потом полное — и повторные полные.</summary>
    public static MythosBookStage NextStage(MythosBookRecord book) =>
        book.Stage == MythosBookStage.NotRead ? MythosBookStage.InitialReading : MythosBookStage.FullStudy;

    /// <summary>
    ///     Прирост Мифов Ктулху. Начальное чтение — МКН. Полное изучение — МКП, если навык
    ///     читателя ниже значения Мифов книги, иначе только МКН (стр. 173). Повторное изучение
    ///     считается так же.
    /// </summary>
    public static int MythosGain(MythosBookRecord book, MythosBookStage stage, int currentMythos)
    {
        var gain = stage == MythosBookStage.FullStudy && currentMythos < book.MythosRating
            ? book.MythosFull
            : book.MythosInitial;

        return stage == MythosBookStage.NotRead ? 0 : Math.Max(0, gain);
    }

    /// <summary>Идёт ли полное изучение по МКП — для подписи «почему столько».</summary>
    public static bool GetsFullGain(MythosBookRecord book, int currentMythos) =>
        currentMythos < book.MythosRating;

    /// <summary>
    ///     Срок следующего полного изучения в неделях: каждое повторное вдвое дольше предыдущего
    ///     (стр. 173). Null — время изучения у книги не указано.
    /// </summary>
    public static int? NextStudyWeeks(MythosBookRecord book)
    {
        if (book.StudyWeeks is not > 0)
            return null;

        var doublings = Math.Clamp(book.FullStudyCount, 0, MaxDoublings);
        return book.StudyWeeks.Value * (1 << doublings);
    }

    /// <summary>
    ///     Навык языка книги на листе: после полного изучения он отмечается для фазы развития
    ///     (стр. 173). Ищется по вхождению названия языка в «Язык, …» — «латынь» находит
    ///     «Язык, иностранный (латынь)». Родной язык без уточнения так не находится, и Хранитель
    ///     отметит его сам.
    /// </summary>
    public static Skill? FindLanguageSkill(Character character, string? language)
    {
        if (string.IsNullOrWhiteSpace(language))
            return null;

        var name = language.Trim();
        return character.Skills.SkillGroups
            .SelectMany(g => g.Skills)
            .FirstOrDefault(s => s.Name.StartsWith(LanguageSkillPrefix, StringComparison.OrdinalIgnoreCase)
                                 && s.Name.Contains(name, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    ///     Заклинание книги для листа: сопоставленное с каталогом — его копия, иначе запись
    ///     с одним названием. Стоимость и время сотворения не придумываются: их впишет Хранитель.
    /// </summary>
    public static Spell ToCharacterSpell(string bookSpellName, Spell? catalogSpell, string bookName)
    {
        if (catalogSpell is not null)
        {
            return new Spell
            {
                Id = Guid.NewGuid(),
                Name = catalogSpell.Name,
                SpellType = catalogSpell.SpellType,
                Cost = catalogSpell.Cost,
                CastingTime = catalogSpell.CastingTime,
                Description = catalogSpell.Description,
                AlternativeNames = catalogSpell.AlternativeNames.ToList()
            };
        }

        return new Spell
        {
            Id = Guid.NewGuid(),
            Name = bookSpellName.Trim().Trim('«', '»', ' '),
            SpellType = string.Empty,
            Description = $"Из книги «{bookName}». В каталоге заклинаний не нашлось — стоимость и время сотворения впишите по описанию Хранителя."
        };
    }

    /// <summary>
    ///     Применяет чтение к листу: потеря рассудка (одной причиной — см.
    ///     <see cref="SanityRules.ApplyLoss" />), прирост Мифов с пересчётом максимума Рассудка,
    ///     выученные заклинания, отметка языка и стадия книги. Порядок важен: рассудок списывается
    ///     с текущего значения, а уже потом новый максимум прижимает остаток.
    /// </summary>
    public static MythosBookReadingEntry Apply(Character character, MythosBookRecord book, MythosBookReading reading)
    {
        var entry = new MythosBookReadingEntry
        {
            Stage = reading.Stage,
            GameDate = string.IsNullOrWhiteSpace(reading.GameDate) ? null : reading.GameDate.Trim(),
            Disbelieved = reading.Disbelieved
        };

        if (!reading.Disbelieved && reading.SanityLoss > 0)
            entry.SanityLost = SanityRules.ApplyLoss(character, reading.SanityLoss);

        var gain = MythosGain(book, reading.Stage, SanityRules.GetMythosValue(character));
        entry.MythosGained = SanityRules.AddMythos(character, gain);

        foreach (var spell in reading.SpellsToLearn)
        {
            if (SpellCatalogMatcher.IsKnown(character.Spells, spell.Name, spell.AlternativeNames))
                continue;

            character.Spells.Add(spell);
            entry.SpellsLearned.Add(spell.Name);
        }

        if (reading.Stage == MythosBookStage.FullStudy
            && reading.MarkLanguageSkill
            && FindLanguageSkill(character, book.Language) is { } language
            && DevelopmentPhaseRules.CanBeChecked(language.Name))
        {
            language.IsUsed = true;
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

/// <summary>
///     Что Хранитель подтвердил в модалке чтения. Потеря рассудка — уже число: бросили здесь
///     или вписали с настоящих костей.
/// </summary>
public sealed record MythosBookReading(
    MythosBookStage Stage,
    int SanityLoss,
    bool Disbelieved,
    string? GameDate,
    IReadOnlyList<Spell> SpellsToLearn,
    bool MarkLanguageSkill);
