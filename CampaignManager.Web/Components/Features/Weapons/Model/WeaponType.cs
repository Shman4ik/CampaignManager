namespace CampaignManager.Web.Components.Features.Weapons.Model;

/// <summary>
///     Перечисление типов оружия
/// </summary>
[Flags]
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