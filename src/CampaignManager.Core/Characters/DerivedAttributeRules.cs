using CampaignManager.Core.Catalogs;

namespace CampaignManager.Core.Characters;

/// <summary>Вычисленное по листу: максимумы, Комплекция, БкУ, Скорость, Уклонение.</summary>
public sealed record DerivedStats(
    int MaxHitPoints,
    int MaxMagicPoints,
    int MaxSanity,
    int MaxLuck,
    int Build,
    string DamageBonus,
    int Move,
    int Dodge);

/// <summary>
/// Вторичные атрибуты главы 3 (стр. 30–31) — <b>единственное</b> место этих формул. В документ они не
/// пишутся: в v1 они хранились в листе, правились руками и молча перезаписывались следующим пересчётом.
/// Напечатанные в книге значения НПС лежат в <see cref="SheetOverrides"/> и побеждают вычисленные.
/// </summary>
public static class DerivedAttributeRules
{
    /// <summary>
    /// Удача никогда не превышает 99 (стр. 93). Начальное значение дальше нигде не используется,
    /// поэтому потолок — не бросок, а 99.
    /// </summary>
    public const int MaxLuck = 99;

    /// <summary>ПЗ = (ТЕЛ + ВЫН) / 10, вниз (стр. 30).</summary>
    public static int ComputeMaxHitPoints(Characteristics c) => (c.Siz + c.Con) / 10;

    /// <summary>ПМ = МОЩ / 5 (стр. 31).</summary>
    public static int ComputeMaxMagicPoints(Characteristics c) => c.Pow / 5;

    /// <summary>Уклонение = половина ЛВК (стр. 57) — база навыка.</summary>
    public static int ComputeDodge(Characteristics c) => c.Dex / 2;

    /// <summary>Скорость 7/8/9 по СИЛ и ЛВК против ТЕЛ, минус 1 за каждое десятилетие с 40 лет (стр. 31).</summary>
    public static int ComputeMoveRate(Characteristics c, int age)
    {
        int move;
        if (c.Str < c.Siz && c.Dex < c.Siz) move = 7;
        else if (c.Str > c.Siz && c.Dex > c.Siz) move = 9;
        else move = 8;

        if (age >= 40)
            move = Math.Max(1, move - ((age - 40) / 10 + 1));

        return move;
    }

    /// <summary>
    /// Таблица I «Бонус к урону и Комплекция» (стр. 31). Свыше 444: каждые следующие 80 пунктов —
    /// +1d6 к БкУ и +1 к Комплекции.
    /// </summary>
    public static (int Build, string DamageBonus) ComputeBuildAndDamageBonus(Characteristics c)
    {
        var sum = c.Str + c.Siz;

        return sum switch
        {
            <= 64 => (-2, "-2"),
            <= 84 => (-1, "-1"),
            <= 124 => (0, "0"),
            <= 164 => (1, "+1D4"),
            <= 204 => (2, "+1D6"),
            <= 284 => (3, "+2D6"),
            <= 364 => (4, "+3D6"),
            <= 444 => (5, "+4D6"),
            _ => ((sum - 365) / 80 + 5, $"+{(sum - 365) / 80 + 4}D6"),
        };
    }

    /// <summary>Максимум ПЗ листа: из книги, если вписан, иначе по формуле.</summary>
    public static int MaxHitPoints(CharacterSheet sheet) =>
        sheet.Overrides.MaxHitPoints ?? ComputeMaxHitPoints(sheet.Characteristics);

    /// <summary>Максимум ПМ листа.</summary>
    public static int MaxMagicPoints(CharacterSheet sheet) =>
        sheet.Overrides.MaxMagicPoints ?? ComputeMaxMagicPoints(sheet.Characteristics);

    /// <summary>Всё вычисляемое по листу разом, с поправками из книги.</summary>
    public static DerivedStats Compute(CharacterSheet sheet, SkillCatalog catalog)
    {
        var c = sheet.Characteristics;
        var o = sheet.Overrides;
        var (build, damageBonus) = ComputeBuildAndDamageBonus(c);

        return new DerivedStats(
            MaxHitPoints(sheet),
            MaxMagicPoints(sheet),
            o.MaxSanity ?? SanityRules.ComputeMaxSanity(sheet, catalog),
            MaxLuck,
            o.Build ?? build,
            o.DamageBonus ?? damageBonus,
            o.Move ?? ComputeMoveRate(c, sheet.Personal.Age),
            sheet.Value(catalog, SkillCodes.Dodge));
    }

    /// <summary>
    /// После правки характеристик или возраста: текущие ПЗ, ПМ, Рассудок и Удача прижимаются к новым
    /// максимумам (не поднимаются), Уклонение ниже новой базы поднимается до неё — вложенные пункты не
    /// трогаются.
    /// </summary>
    public static void Normalize(CharacterSheet sheet, SkillCatalog catalog)
    {
        var derived = Compute(sheet, catalog);
        var current = sheet.Current;

        current.HitPoints = Math.Min(current.HitPoints, derived.MaxHitPoints);
        current.MagicPoints = Math.Min(current.MagicPoints, derived.MaxMagicPoints);
        current.Sanity = Math.Min(current.Sanity, derived.MaxSanity);
        current.Luck = Math.Min(current.Luck, derived.MaxLuck);

        if (sheet.Entry(catalog, SkillCodes.Dodge) is { } dodge)
            dodge.Value = Math.Max(dodge.Value, ComputeDodge(sheet.Characteristics));
    }

    /// <summary>
    /// Новый лист: ПЗ и ПМ на максимуме, Рассудок = МОЩ, но не выше максимума (стр. 31). Удачу не
    /// бросает — её бросают помощник или игрок.
    /// </summary>
    public static void InitializeNewSheet(CharacterSheet sheet, SkillCatalog catalog)
    {
        Normalize(sheet, catalog);

        var derived = Compute(sheet, catalog);
        sheet.Current.HitPoints = derived.MaxHitPoints;
        sheet.Current.MagicPoints = derived.MaxMagicPoints;
        sheet.Current.Sanity = Math.Min(sheet.Characteristics.Pow, derived.MaxSanity);
    }
}

/// <summary>Половина и пятая часть значения (стр. 30) — с округлением вниз, как на бланке.</summary>
public static class CharacteristicMath
{
    public static int Half(int value) => value / 2;

    public static int Fifth(int value) => value / 5;
}
