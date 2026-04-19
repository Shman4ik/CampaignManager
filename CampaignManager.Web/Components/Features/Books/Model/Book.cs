using CampaignManager.Web.Model;

namespace CampaignManager.Web.Components.Features.Books.Model;

/// <summary>
///     Книга Мифов Ктулху или книга по оккультизму (раздел вики).
///     Версии одной книги (Некрономикон, Книга Эйбона и т. п.) хранятся как отдельные записи.
/// </summary>
public class Book : BaseDataBaseEntity, INamedEntity
{
    /// <summary>Название книги (для версий — с уточнением, например «Некрономикон (латинский перевод Ворма)»).</summary>
    public required string Name { get; set; }

    /// <summary>Категория книги: книга Мифов или книга по оккультизму.</summary>
    public required BookType BookType { get; set; }

    /// <summary>Альтернативные названия и переводы (например «Al Azif», «Liber Necronomicon»).</summary>
    public List<string> AlternativeNames { get; set; } = [];

    /// <summary>Язык оригинала или данного перевода (например «латынь», «арабский», «английский»).</summary>
    public string? Language { get; set; }

    /// <summary>Год или век написания (свободный текст: «1228», «XV в.», «ок. 730 г. н. э.»).</summary>
    public string? Year { get; set; }

    /// <summary>Автор, переводчик или «неизвестен».</summary>
    public string? Author { get; set; }

    /// <summary>Потеря рассудка от прочтения (формула кубов: «1d4», «2d10», «нет»).</summary>
    public string? SanityLoss { get; set; }

    /// <summary>МКН — прибавка к «Мифы Ктулху» за начальное чтение. Null для книг по оккультизму.</summary>
    public int? CthulhuMythosInitial { get; set; }

    /// <summary>МКП — прибавка к «Мифы Ктулху» за полное изучение. Null для книг по оккультизму.</summary>
    public int? CthulhuMythosFull { get; set; }

    /// <summary>ЗМ — значение Мифов (0–100). Null для книг по оккультизму.</summary>
    public int? MythosRating { get; set; }

    /// <summary>Приблизительное время полного изучения в неделях. Null если неизвестно.</summary>
    public int? StudyWeeks { get; set; }

    /// <summary>Бонус к навыку «Оккультизм» (в процентах). Null для книг Мифов.</summary>
    public int? OccultismBonus { get; set; }

    /// <summary>Подробное описание: внешний вид, история, содержание.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Возможные заклинания в книге (список названий). Пустой у книг по оккультизму.</summary>
    public List<string> PossibleSpells { get; set; } = [];

    /// <summary>URL изображения обложки книги.</summary>
    public string? ImageUrl { get; set; }
}
