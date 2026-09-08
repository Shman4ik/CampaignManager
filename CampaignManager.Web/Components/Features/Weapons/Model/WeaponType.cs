namespace CampaignManager.Web.Components.Features.Weapons.Model;

/// <summary>
///     Перечисление типов оружия — по разделам таблицы XVII «Оружие» (стр. 399–402).
///     <para>
///         Значения нумеруются подряд, а не степенями двойки: это взаимоисключающие
///         категории, а не набор флагов. Раньше на перечислении стоял <c>[Flags]</c>,
///         и <c>Pistols | Rifles</c> молча давал <see cref="Shotguns" />.
///         Нужно несколько типов сразу — передавайте коллекцию.
///     </para>
/// </summary>
public enum WeaponType
{
    /// <summary>
    ///     Холодное оружие
    /// </summary>
    Melee,

    /// <summary>
    ///     Пистолеты
    /// </summary>
    Pistols,

    /// <summary>
    ///     Винтовки
    /// </summary>
    Rifles,

    /// <summary>
    ///     Дробовики
    /// </summary>
    Shotguns,

    /// <summary>
    ///     Автоматические винтовки
    /// </summary>
    AssaultRifles,

    /// <summary>
    ///     Пистолеты-пулемёты
    /// </summary>
    SubmachineGuns,

    /// <summary>
    ///     Пулемёты
    /// </summary>
    MachineGuns,

    /// <summary>
    ///     Взрывчатка, тяжёлое вооружение
    /// </summary>
    ExplosivesAndHeavyWeapons,

    /// <summary>
    ///     Оружие иного типа, не входящее в перечисленные категории
    /// </summary>
    Other
}