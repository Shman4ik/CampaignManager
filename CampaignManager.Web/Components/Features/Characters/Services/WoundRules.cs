using CampaignManager.Web.Components.Features.Characters.Model;

namespace CampaignManager.Web.Components.Features.Characters.Services;

/// <summary>
///     Раны и лечение («Зов Ктулху» 7e, стр. 117).
/// </summary>
public static class WoundRules
{
    /// <summary>
    ///     Урон, с которого рана считается серьёзной: «равен или больше половины максимальных ПЗ».
    ///     При 13 ПЗ половина — 6,5, то есть серьёзная рана начинается с 7, поэтому округляем вверх.
    /// </summary>
    public static int MajorWoundThreshold(int maxHitPoints) => Math.Max(1, (maxHitPoints + 1) / 2);

    public static bool IsMajorWound(int damage, int maxHitPoints) =>
        damage >= MajorWoundThreshold(maxHitPoints);

    /// <summary>
    ///     Применяет одну атаку: снимает ПЗ, отмечает серьёзную рану и выводит состояние.
    ///     Возвращает true, если рана оказалась серьёзной.
    /// </summary>
    public static bool ApplyDamage(Character character, int damage)
    {
        if (damage <= 0)
            return false;

        var hitPoints = character.DerivedAttributes.HitPoints;
        var major = IsMajorWound(damage, hitPoints.MaxValue);

        hitPoints.Value = Math.Max(0, hitPoints.Value - damage);

        if (major)
            character.State.HasSeriousInjury = true;

        UpdateConsciousness(character);
        return major;
    }

    /// <summary>
    ///     0 ПЗ без серьёзной раны — без сознания; 0 ПЗ с серьёзной раной — при смерти (стр. 118).
    ///     Пока ПЗ выше нуля, оба состояния снимаются: сыщик снова на ногах.
    /// </summary>
    public static void UpdateConsciousness(Character character)
    {
        var down = character.DerivedAttributes.HitPoints.Value <= 0;

        character.State.IsDying = down && character.State.HasSeriousInjury;
        character.State.IsUnconscious = down && !character.State.HasSeriousInjury;
    }
}
