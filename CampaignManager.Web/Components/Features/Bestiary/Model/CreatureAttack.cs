namespace CampaignManager.Web.Components.Features.Bestiary.Model;

/// <summary>
///     Структурированное описание атаки существа
/// </summary>
public class CreatureAttack
{
    /// <summary>
    ///     Название атаки (например, "Ужасная жадная пасть")
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    ///     Навык атаки в процентах (например, 75)
    /// </summary>
    public int SkillValue { get; set; } = 50;

    /// <summary>
    ///     Формула урона (например, "1D8", "2D6+1D4")
    /// </summary>
    public string DamageFormula { get; set; } = "1D3";

    /// <summary>
    ///     Ближний бой (true) или дальний бой (false)
    /// </summary>
    public bool IsMelee { get; set; } = true;

    /// <summary>
    ///     Количество атак данного типа за раунд
    /// </summary>
    public int AttacksPerRound { get; set; } = 1;

    /// <summary>
    ///     Свободное описание эффектов (яд, захват и т.д.)
    /// </summary>
    public string? Description { get; set; }
}
