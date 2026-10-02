namespace CampaignManager.Core.Characters;

/// <summary>Раны и лечение (стр. 117–118).</summary>
public static class WoundRules
{
    /// <summary>
    /// Урон, с которого рана серьёзная: «равен или больше половины максимальных ПЗ». При 13 ПЗ половина —
    /// 6,5, рана начинается с 7: округление вверх.
    /// </summary>
    public static int MajorWoundThreshold(int maxHitPoints) => Math.Max(1, (maxHitPoints + 1) / 2);

    public static bool IsMajorWound(int damage, int maxHitPoints) => damage >= MajorWoundThreshold(maxHitPoints);

    /// <summary>
    /// Одна атака: снимает ПЗ, отмечает серьёзную рану, выводит состояние. Возвращает true, если рана
    /// серьёзная. Урон не меньше максимума ПЗ здесь — «при смерти», а не смерть: расхождение с боем
    /// записано в rules-findings (F-S02) и решается в T2.6 вместе с конвейером ран боя.
    /// </summary>
    public static bool ApplyDamage(CharacterSheet sheet, int damage)
    {
        if (damage <= 0)
            return false;

        var major = IsMajorWound(damage, DerivedAttributeRules.MaxHitPoints(sheet));
        sheet.Current.HitPoints = Math.Max(0, sheet.Current.HitPoints - damage);

        if (major)
            sheet.Condition.MajorWound = true;

        UpdateConsciousness(sheet);
        return major;
    }

    /// <summary>
    /// 0 ПЗ без серьёзной раны — без сознания, с серьёзной — при смерти (стр. 118). Пока ПЗ выше нуля,
    /// оба состояния снимаются.
    /// </summary>
    public static void UpdateConsciousness(CharacterSheet sheet) =>
        (sheet.Condition.Unconscious, sheet.Condition.Dying) = Consciousness(sheet.Current.HitPoints, sheet.Condition.MajorWound);

    /// <summary>
    /// То же правило для любого счётчика ПЗ (лист и снимок участника сцены): 0 ПЗ без серьёзной раны — без сознания,
    /// с раной — при смерти.
    /// </summary>
    public static (bool Unconscious, bool Dying) Consciousness(int hitPoints, bool majorWound)
    {
        var down = hitPoints <= 0;
        return (down && !majorWound, down && majorWound);
    }
}
