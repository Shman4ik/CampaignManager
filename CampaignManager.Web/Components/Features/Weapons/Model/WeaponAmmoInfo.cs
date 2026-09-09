namespace CampaignManager.Web.Components.Features.Weapons.Model;

/// <summary>
///     Структурированный боезапас, разобранный из текстового поля
///     <see cref="Weapon.Ammo" /> (колонка «Боезапас» таблицы XVII).
///     Заполняется <c>WeaponStatsParser.ParseAmmo</c>; хранится в БД как JSONB.
/// </summary>
public sealed class WeaponAmmoInfo
{
    /// <summary>
    ///     Ёмкость магазина. Для оружия с несколькими вариантами магазина —
    ///     первый (наименьший штатный) из <see cref="CapacityOptions" />.
    /// </summary>
    public int? Capacity { get; set; }

    /// <summary>
    ///     Варианты ёмкости: «20/30/32» → [20, 30, 32]. Null, если вариант один.
    /// </summary>
    public List<int>? CapacityOptions { get; set; }

    /// <summary>Одноразовое: «Только 1», «Однораз.», «Одноразовая».</summary>
    public bool IsSingleUse { get; set; }

    /// <summary>Ленточное питание: «Автоподача» — ёмкость ограничена только лентой.</summary>
    public bool IsBeltFed { get; set; }

    /// <summary>Боеприпас поставляется отдельно: «Отдельно» (гранатомёты, огнемёты).</summary>
    public bool IsSuppliedSeparately { get; set; }

    /// <summary>Оригинальная строка до разбора.</summary>
    public string RawText { get; set; } = string.Empty;

    /// <summary>Удалось ли разобрать текст.</summary>
    public bool IsParsed { get; set; }

    public override string ToString() => RawText;
}
