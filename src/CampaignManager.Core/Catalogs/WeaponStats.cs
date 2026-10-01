namespace CampaignManager.Core.Catalogs;

/// <summary>Как таблица XVII задаёт дальность.</summary>
public enum WeaponRangeKind
{
    /// <summary>Не указана или неприменима («Нет», пусто).</summary>
    None,

    /// <summary>Ближний бой: «Касание», «контакт».</summary>
    Touch,

    /// <summary>Базовая дальность в метрах: «15 метров».</summary>
    Meters,

    /// <summary>Дробовики: несколько порогов — «10/20/50 метров».</summary>
    RangeBands,

    /// <summary>Метательное: «СИЛ / 5 метров» — от СИЛ.</summary>
    StrengthThrow,

    /// <summary>Стационарная закладка: «На месте».</summary>
    Emplaced,
}

/// <summary>Разобранная колонка «Дальность».</summary>
public sealed class WeaponRangeInfo
{
    public WeaponRangeKind Kind { get; set; } = WeaponRangeKind.None;

    /// <summary>Базовая дальность в метрах; у дробовика — первый порог.</summary>
    public int? BaseMeters { get; set; }

    /// <summary>Пороги дробовика: «10/20/50 метров» → [10, 20, 50].</summary>
    public List<int>? Bands { get; set; }

    /// <summary>Делитель СИЛ метательного: «СИЛ / 5 метров» → 5.</summary>
    public int? ThrowDivisor { get; set; }

    public string RawText { get; set; } = string.Empty;

    public bool IsParsed { get; set; }

    public override string ToString() => RawText;
}

/// <summary>Разобранная колонка «Атак».</summary>
public sealed class WeaponAttacksInfo
{
    /// <summary>Выстрелов за раунд при обычной стрельбе.</summary>
    public int? ShotsPerRound { get; set; }

    /// <summary>Предел за одну проверку: «1 (3)» → 3.</summary>
    public int? MaxShotsPerRound { get; set; }

    /// <summary>Медленное оружие: «1/4» — раз в 4 раунда.</summary>
    public int? RoundsPerAttack { get; set; }

    public bool AllowsBurst { get; set; }

    public int? BurstSize { get; set; }

    public bool AllowsFullAuto { get; set; }

    public bool IsSingleUse { get; set; }

    public string RawText { get; set; } = string.Empty;

    public bool IsParsed { get; set; }

    public override string ToString() => RawText;
}

/// <summary>Разобранная колонка «Боезапас».</summary>
public sealed class WeaponAmmoInfo
{
    /// <summary>Ёмкость магазина; у вариантов — первый.</summary>
    public int? Capacity { get; set; }

    /// <summary>Варианты магазина: «20/30/32».</summary>
    public List<int>? CapacityOptions { get; set; }

    public bool IsSingleUse { get; set; }

    /// <summary>«Автоподача» — лента, ёмкости нет.</summary>
    public bool IsBeltFed { get; set; }

    /// <summary>«Отдельно» — боеприпас покупается сам по себе.</summary>
    public bool IsSuppliedSeparately { get; set; }

    public string RawText { get; set; } = string.Empty;

    public bool IsParsed { get; set; }

    public override string ToString() => RawText;
}

/// <summary>Разобранная колонка «Стоимость»: «1920-е / современность».</summary>
public sealed class WeaponCostInfo
{
    public decimal? Cost1920 { get; set; }

    public decimal? CostModern { get; set; }

    /// <summary>«от $100» или вилка цены — ориентир, а не точная цена.</summary>
    public bool IsApproximate1920 { get; set; }

    public bool IsApproximateModern { get; set; }

    /// <summary>Эпоха записана явно и без цены («—», «Нет»).</summary>
    public bool Unavailable1920 { get; set; }

    public bool UnavailableModern { get; set; }

    public string RawText { get; set; } = string.Empty;

    public bool IsParsed { get; set; }

    public override string ToString() => RawText;
}
