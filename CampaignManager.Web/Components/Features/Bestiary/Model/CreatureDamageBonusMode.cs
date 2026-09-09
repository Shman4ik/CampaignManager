namespace CampaignManager.Web.Components.Features.Bestiary.Model;

/// <summary>
///     Как строка урона в статблоке обращается со средним бонусом к урону (БкУ).
///     Книга печатает это словами прямо в строке атаки (стр. 278).
/// </summary>
public enum CreatureDamageBonusMode
{
    /// <summary>«урон 1d3» — бонус не добавляется.</summary>
    None,

    /// <summary>«урон 2d6 + БкУ» — обычный случай ближнего боя.</summary>
    Full,

    /// <summary>«урон 2d3 + ½ БкУ» — половина бонуса, как у акулы.</summary>
    Half,

    /// <summary>
    ///     «урон равен БкУ» — весь урон и есть бонус, своих костей у атаки нет
    ///     (шоггот, тёмная молодь, Старец).
    /// </summary>
    OnlyBonus
}
