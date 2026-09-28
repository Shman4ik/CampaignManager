namespace CampaignManager.Web.Components.Features.Characters.Model;

/// <summary>
///     Докуда сыщик дошёл в чтении книги Мифов (стр. 171–173).
///     Хранится в JSONB числом — новые стадии добавлять только в конец.
/// </summary>
public enum MythosBookStage
{
    /// <summary>Книга у сыщика, но он её ещё не открывал.</summary>
    NotRead,

    /// <summary>Начальное чтение: МКН к Мифам и потеря рассудка, известен список заклинаний.</summary>
    InitialReading,

    /// <summary>Полное изучение (хотя бы одно): МКП, пока Мифы сыщика ниже ЗМ книги.</summary>
    FullStudy
}

/// <summary>
///     Книга Мифов на листе сыщика (гл. 9 «Магия», стр. 171–174; гл. 11).
///     <para>
///         Хранит ссылку на каталог и <b>свой экземпляр</b> чисел книги: название, потерю рассудка,
///         МКН/МКП, ЗМ, время изучения и список заклинаний снимаются с каталога при добавлении и
///         дальше живут на листе. Так велит сама книга правил: экземпляры одной книги различаются,
///         за оригинальное издание Хранитель добавляет процентов к Мифам (стр. 222), — и правка
///         числа на листе не должна менять справочник у всех. Заодно запись переживает удаление
///         книги из каталога, а книгу из сценария, которой в каталоге нет, можно завести руками.
///     </para>
/// </summary>
public class MythosBookRecord
{
    /// <summary>Идентификатор строки на листе (для @key и поиска), не книги.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Книга каталога <c>games."Books"</c>; null — Хранитель завёл книгу руками.</summary>
    public Guid? BookId { get; set; }

    /// <summary>Слепок названия на момент добавления.</summary>
    public string Name { get; set; } = "";

    public string? Language { get; set; }

    /// <summary>Потеря рассудка от чтения — формула костей («2d4»), как в каталоге.</summary>
    public string? SanityLoss { get; set; }

    /// <summary>МКН — прибавка к Мифам Ктулху за начальное чтение.</summary>
    public int MythosInitial { get; set; }

    /// <summary>МКП — прибавка за полное изучение, пока Мифы читателя ниже ЗМ.</summary>
    public int MythosFull { get; set; }

    /// <summary>ЗМ — значение Мифов книги (стр. 173).</summary>
    public int MythosRating { get; set; }

    /// <summary>Время первого полного изучения в неделях; каждое следующее — вдвое дольше.</summary>
    public int? StudyWeeks { get; set; }

    /// <summary>Возможные заклинания книги — как их записал каталог.</summary>
    public List<string> PossibleSpells { get; set; } = [];

    public MythosBookStage Stage { get; set; }

    /// <summary>Сколько полных изучений завершено: от него зависит срок следующего.</summary>
    public int FullStudyCount { get; set; }

    public string Note { get; set; } = "";

    /// <summary>Что уже применено к листу — по записи на каждое чтение.</summary>
    public List<MythosBookReadingEntry> Readings { get; set; } = [];
}

/// <summary>
///     Одно применённое чтение: стадия, игровое время и что оно дало листу. Хранитель видит,
///     откуда взялись проценты Мифов и потерянный рассудок.
/// </summary>
public class MythosBookReadingEntry
{
    public MythosBookStage Stage { get; set; }

    /// <summary>Игровое время свободным текстом («март 1925», «после визита в Мискатоник»).</summary>
    public string? GameDate { get; set; }

    public int SanityLost { get; set; }
    public int MythosGained { get; set; }

    /// <summary>Сыщик не верит в Мифы — прочитанное растит навык, но рассудка не отнимает (стр. 177).</summary>
    public bool Disbelieved { get; set; }

    /// <summary>Названия заклинаний, добавленных на лист этим чтением.</summary>
    public List<string> SpellsLearned { get; set; } = [];

    /// <summary>Реальное время применения — порядок записей, не игровая дата.</summary>
    public DateTime AppliedAt { get; set; } = DateTime.UtcNow;
}
