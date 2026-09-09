using CampaignManager.Web.Model;
using System.ComponentModel.DataAnnotations;

namespace CampaignManager.Web.Components.Features.Weapons.Model;

/// <summary>
///     Класс для всех видов оружия
/// </summary>
public class Weapon : BaseDataBaseEntity, INamedEntity
{
    public Weapon()
    {
        Type = WeaponType.Melee;
    }

    /// <summary>
    ///     Тип оружия (ближнего или дальнего боя)
    /// </summary>
    [Required]
    public WeaponType Type { get; set; }

    /// <summary>
    ///     Название оружия
    /// </summary>
    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    ///     Навык, используемый для владения оружием
    /// </summary>
    [Required]
    [StringLength(50)]
    public string Skill { get; set; } = string.Empty;

    /// <summary>
    ///     Признак оружия эпохи 1920-х годов
    /// </summary>
    public bool Is1920 { get; set; }

    /// <summary>
    ///     Признак современного оружия
    /// </summary>
    public bool IsModern { get; set; }

    /// <summary>
    ///     Редкое оружие: устаревший образец, запрещённое или коллекционное (колонка
    ///     «Встречается» таблицы XVII). Ортогонально эпохе: «1920-е, редко» — это
    ///     <see cref="Is1920" /> вместе с этим флагом.
    /// </summary>
    public bool IsRare { get; set; }

    /// <summary>
    ///     Урон, наносимый оружием
    /// </summary>
    [Required]
    [StringLength(50)]
    public string Damage { get; set; } = string.Empty;

    /// <summary>
    ///     Дальность действия оружия
    /// </summary>
    [Required]
    [StringLength(50)]
    public string Range { get; set; } = string.Empty;

    /// <summary>
    ///     Количество атак, совершаемых оружием
    /// </summary>
    [Required]
    [StringLength(40)]
    public string Attacks { get; set; } = string.Empty;

    /// <summary>
    ///     Стоимость оружия
    /// </summary>
    [StringLength(20)]
    public string Cost { get; set; } = string.Empty;

    /// <summary>
    ///     Дополнительные примечания к оружию
    /// </summary>
    [StringLength(500)]
    public string Notes { get; set; } = string.Empty;

    /// <summary>
    ///     Тип боеприпасов (для оружия дальнего боя)
    /// </summary>
    [StringLength(20)]
    public string Ammo { get; set; } = string.Empty;

    /// <summary>
    ///     Шанс осечки (для оружия дальнего боя)
    /// </summary>
    [StringLength(10)]
    public string Malfunction { get; set; } = string.Empty;

    /// <summary>
    ///     Является ли оружие проникающим по правилам CoC 7e.
    ///     Проникающее оружие при чрезвычайном успехе наносит дополнительный бросок урона.
    ///     Примеры: ножи, клинки, копья, всё огнестрельное оружие.
    /// </summary>
    public bool IsImpaling { get; set; }

    /// <summary>
    ///     Структурированная информация об уроне, полученная автоматическим парсингом поля Damage.
    ///     Null означает, что парсинг ещё не выполнялся. IsParsed = false — текст не удалось распарсить.
    ///     Хранится в БД как JSONB.
    /// </summary>
    public WeaponDamageInfo? DamageInfo { get; set; }

    /// <summary>
    ///     Структурированная дальность, полученная разбором поля <see cref="Range" />.
    ///     Null — разбор ещё не выполнялся; потребители читают через <c>WeaponStatsReader</c>.
    ///     Хранится в БД как JSONB.
    /// </summary>
    public WeaponRangeInfo? RangeInfo { get; set; }

    /// <summary>
    ///     Структурированное число атак, полученное разбором поля <see cref="Attacks" />.
    ///     Хранится в БД как JSONB.
    /// </summary>
    public WeaponAttacksInfo? AttacksInfo { get; set; }

    /// <summary>
    ///     Структурированный боезапас, полученный разбором поля <see cref="Ammo" />.
    ///     Хранится в БД как JSONB.
    /// </summary>
    public WeaponAmmoInfo? AmmoInfo { get; set; }

    /// <summary>
    ///     Структурированная стоимость, полученная разбором поля <see cref="Cost" />.
    ///     Хранится в БД как JSONB.
    /// </summary>
    public WeaponCostInfo? CostInfo { get; set; }

    /// <summary>
    ///     Порог осечки числом (стр. 113): бросок ≥ порога — оружие заклинило.
    ///     Null — осечки у оружия нет. Разобран из <see cref="Malfunction" />, где
    ///     то же значение записано текстом («100», «00», пустая строка).
    /// </summary>
    public int? MalfunctionThreshold { get; set; }

    /// <summary>
    ///     Идентификатор навыка из справочника <c>games."Skills"</c>.
    ///     <para>
    ///         Внешнего ключа и навигационного свойства намеренно нет: этот же тип лежит
    ///         внутри JSONB листов персонажей, где ограничение всё равно не действует,
    ///         а удаление навыка не должно ронять каталог оружия. Каноническое имя
    ///         продолжает храниться денормализованно в <see cref="Skill" />.
    ///     </para>
    /// </summary>
    public Guid? SkillId { get; set; }

    /// <summary>
    ///     Ссылка на каталожную запись, с которой снята эта копия.
    ///     Заполнена только у копии в листе персонажа; у самой каталожной строки — null.
    ///     Самодельное оружие Хранителя также несёт null.
    /// </summary>
    public Guid? CatalogWeaponId { get; set; }
}
