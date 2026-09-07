using CampaignManager.Web.Components.Features.Characters.Model;

namespace CampaignManager.Web.Components.Features.Characters.Services;

/// <summary>
///     Вторичные атрибуты из главы 3 «Создание сыщиков» («Зов Ктулху» 7e, стр. 30–31).
///     Единственное место, где живут эти формулы: и генератор, и лист персонажа считают по нему,
///     иначе отредактированный вручную лист расходится с правилами.
/// </summary>
public static class DerivedAttributeRules
{
    /// <summary>
    ///     Удача меняется по ходу игры, но никогда не превышает 99 (стр. 93).
    ///     Начальное значение Удачи дальше нигде не используется, поэтому потолок — не бросок, а 99.
    /// </summary>
    public const int MaxLuck = 99;

    /// <summary>Пункты здоровья = (ТЕЛ + ВЫН) / 10, округление вниз (стр. 30).</summary>
    public static int ComputeMaxHitPoints(Characteristics c) =>
        (c.Size.Regular + c.Constitution.Regular) / 10;

    /// <summary>Пункты магии = МОЩ / 5 (стр. 31).</summary>
    public static int ComputeMaxMagicPoints(Characteristics c) => c.Power.Regular / 5;

    /// <summary>Уклонение = половина ЛВК (стр. 57).</summary>
    public static int ComputeDodge(Characteristics c) => c.Dexterity.Regular / 2;

    /// <summary>
    ///     Скорость: 7/8/9 по сравнению СИЛ и ЛВК с ТЕЛ, минус 1 за каждое десятилетие с 40 лет (стр. 31).
    /// </summary>
    public static int ComputeMoveRate(Characteristics c, int age)
    {
        var str = c.Strength.Regular;
        var dex = c.Dexterity.Regular;
        var siz = c.Size.Regular;

        int move;
        if (str < siz && dex < siz) move = 7;
        else if (str > siz && dex > siz) move = 9;
        else move = 8;

        if (age >= 40)
            move = Math.Max(1, move - ((age - 40) / 10 + 1));

        return move;
    }

    /// <summary>
    ///     Таблица I «Бонус к урону и Комплексия» (стр. 31).
    ///     Свыше 444: за каждые следующие 80 пунктов +1d6 к БкУ и +1 к Комплексии.
    /// </summary>
    public static (string Build, string DamageBonus) ComputeBuildAndDamageBonus(Characteristics c)
    {
        var sum = c.Strength.Regular + c.Size.Regular;

        return sum switch
        {
            <= 64 => ("-2", "-2"),
            <= 84 => ("-1", "-1"),
            <= 124 => ("0", "0"),
            <= 164 => ("1", "+1D4"),
            <= 204 => ("2", "+1D6"),
            <= 284 => ("3", "+2D6"),
            <= 364 => ("4", "+3D6"),
            <= 444 => ("5", "+4D6"),
            _ => ($"{(sum - 365) / 80 + 5}", $"+{(sum - 365) / 80 + 4}D6")
        };
    }

    /// <summary>
    ///     Пересчитывает всё, что выводится из характеристик, на уже существующем листе.
    ///     Текущие ПЗ/ПМ не сбрасываются — только прижимаются к новому максимуму;
    ///     текущий Рассудок не трогаем вовсе (его максимум считает <see cref="SanityRules" />).
    /// </summary>
    public static void Recalculate(Character character)
    {
        var c = character.Characteristics;

        foreach (var value in EnumerateCharacteristics(c))
            value.UpdateDerived();

        var maxHp = ComputeMaxHitPoints(c);
        character.DerivedAttributes.HitPoints.MaxValue = maxHp;
        character.DerivedAttributes.HitPoints.Value = Math.Min(character.DerivedAttributes.HitPoints.Value, maxHp);

        var maxMp = ComputeMaxMagicPoints(c);
        character.DerivedAttributes.MagicPoints.MaxValue = maxMp;
        character.DerivedAttributes.MagicPoints.Value = Math.Min(character.DerivedAttributes.MagicPoints.Value, maxMp);

        character.DerivedAttributes.Luck.MaxValue = MaxLuck;
        character.DerivedAttributes.Luck.Value = Math.Min(character.DerivedAttributes.Luck.Value, MaxLuck);

        var maxSanity = SanityRules.ComputeMaxSanity(character);
        character.DerivedAttributes.Sanity.MaxValue = maxSanity;
        character.DerivedAttributes.Sanity.Value = Math.Min(character.DerivedAttributes.Sanity.Value, maxSanity);

        var (build, damageBonus) = ComputeBuildAndDamageBonus(c);
        character.PersonalInfo.Build = build;
        character.PersonalInfo.DamageBonus = damageBonus;
        character.PersonalInfo.MoveSpeed = ComputeMoveRate(c, character.PersonalInfo.Age);

        ApplyDodge(character, ComputeDodge(c));
    }

    /// <summary>
    ///     Заполняет вторичные атрибуты чистого листа: ПЗ и ПМ на максимуме, Рассудок = МОЩ (стр. 31).
    ///     Удачу здесь не бросаем — её кидает генератор или сам игрок.
    /// </summary>
    public static void InitializeNewSheet(Character character)
    {
        Recalculate(character);

        character.DerivedAttributes.HitPoints.Value = character.DerivedAttributes.HitPoints.MaxValue;
        character.DerivedAttributes.MagicPoints.Value = character.DerivedAttributes.MagicPoints.MaxValue;
        character.DerivedAttributes.Sanity.Value =
            Math.Min(character.Characteristics.Power.Regular, character.DerivedAttributes.Sanity.MaxValue);
    }

    /// <summary>
    ///     Чинит сохранённые листы, где потолок Удачи записан равным стартовому броску.
    ///     Трогаем только Удачу: остальные максимумы Хранитель мог поправить руками осознанно.
    /// </summary>
    public static void NormalizeLuckCap(Character character)
    {
        character.DerivedAttributes.Luck.MaxValue = MaxLuck;
        character.DerivedAttributes.Luck.Value = Math.Min(character.DerivedAttributes.Luck.Value, MaxLuck);
    }

    /// <summary>
    ///     Уклонение живёт в двух местах — в боевых параметрах и в навыке. Держим их одинаковыми:
    ///     разойдясь, они дают на листе два разных шанса уклониться.
    /// </summary>
    public static void ApplyDodge(Character character, int dodge)
    {
        character.PersonalInfo.Dodge = dodge;

        var skill = character.Skills.SkillGroups
            .SelectMany(g => g.Skills)
            .FirstOrDefault(s => string.Equals(s.Name, "Уклонение", StringComparison.Ordinal));

        if (skill is null)
            return;

        // Вложенные пункты не трогаем: навык мог быть поднят выше базы за счёт очков.
        if (skill.Value.Regular < dodge)
        {
            skill.Value.Regular = dodge;
            skill.Value.UpdateDerived();
        }

        character.PersonalInfo.Dodge = skill.Value.Regular;
    }

    private static IEnumerable<AttributeValue> EnumerateCharacteristics(Characteristics c)
    {
        yield return c.Strength;
        yield return c.Dexterity;
        yield return c.Intelligence;
        yield return c.Constitution;
        yield return c.Appearance;
        yield return c.Power;
        yield return c.Size;
        yield return c.Education;
    }
}
