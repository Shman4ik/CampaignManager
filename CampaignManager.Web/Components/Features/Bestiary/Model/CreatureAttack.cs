using System.Text.Json.Serialization;

namespace CampaignManager.Web.Components.Features.Bestiary.Model;

/// <summary>
///     Одна строка раздела «Бой» из статблока существа: «Ближний бой 60% (30/12),
///     урон 2d6 + БкУ» (гл. 14, стр. 278).
/// </summary>
public class CreatureAttack
{
    /// <summary>
    ///     Название атаки («Ближний бой», «Захват», «Ужасная жадная пасть»).
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    ///     Навык атаки в процентах (например, 75).
    /// </summary>
    public int SkillValue { get; set; } = 50;

    /// <summary>
    ///     Формула урона (например, "1D8", "2D6+1D4"). Для атак, у которых своих костей
    ///     нет («урон равен БкУ»), здесь «0», а весь урон приходит из
    ///     <see cref="CreatureDamageBonusMode.OnlyBonus" />.
    /// </summary>
    public string DamageFormula { get; set; } = "1D3";

    /// <summary>
    ///     Чем является эта строка: атакой, манёвром или особым умением.
    /// </summary>
    public CreatureAttackKind Kind { get; set; } = CreatureAttackKind.Melee;

    /// <summary>
    ///     Как к урону применяется средний бонус к урону существа.
    /// </summary>
    public CreatureDamageBonusMode DamageBonus { get; set; } = CreatureDamageBonusMode.None;

    /// <summary>
    ///     Ближний бой (true) или дальний бой (false). Вычисляется из <see cref="Kind" />:
    ///     манёвр существо совершает навыком Ближний бой (стр. 279), поэтому он тоже
    ///     считается ближним. В JSON не пишется — источником остаётся <see cref="Kind" />.
    /// </summary>
    [JsonIgnore]
    public bool IsMelee => Kind is not CreatureAttackKind.Ranged;

    /// <summary>
    ///     Свободное описание эффектов (яд, захват и т.д.)
    /// </summary>
    public string? Description { get; set; }
}
