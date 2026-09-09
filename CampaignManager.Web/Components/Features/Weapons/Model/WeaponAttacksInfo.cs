namespace CampaignManager.Web.Components.Features.Weapons.Model;

/// <summary>
///     Структурированное число атак, разобранное из текстового поля
///     <see cref="Weapon.Attacks" /> (колонка «Атак» таблицы XVII).
///     Заполняется <c>WeaponStatsParser.ParseAttacks</c>; хранится в БД как JSONB.
/// </summary>
public sealed class WeaponAttacksInfo
{
    /// <summary>
    ///     Сколько выстрелов (ударов) оружие делает за раунд при обычной стрельбе.
    ///     Null, если оружие стреляет только очередями либо разбор не удался.
    /// </summary>
    public int? ShotsPerRound { get; set; }

    /// <summary>
    ///     Предел выстрелов за раунд при стрельбе на скорость: «1 (3)» → 3, «1 или 2» → 2.
    ///     Null, когда предел совпадает с <see cref="ShotsPerRound" />.
    /// </summary>
    public int? MaxShotsPerRound { get; set; }

    /// <summary>
    ///     Раундов на одну атаку для медленного оружия: «1/4» → 4 (выстрел раз в 4 раунда).
    ///     Null для оружия, стреляющего каждый раунд.
    /// </summary>
    public int? RoundsPerAttack { get; set; }

    /// <summary>Оружие умеет стрелять короткими очередями («очередями по 3»).</summary>
    public bool AllowsBurst { get; set; }

    /// <summary>Длина очереди, если <see cref="AllowsBurst" />.</summary>
    public int? BurstSize { get; set; }

    /// <summary>Оружие умеет вести непрерывный огонь («непр. огонь»).</summary>
    public bool AllowsFullAuto { get; set; }

    /// <summary>Одноразовое применение: «Однораз.», «На месте» (закладка, граната).</summary>
    public bool IsSingleUse { get; set; }

    /// <summary>Оригинальная строка до разбора.</summary>
    public string RawText { get; set; } = string.Empty;

    /// <summary>Удалось ли разобрать текст.</summary>
    public bool IsParsed { get; set; }

    public override string ToString() => RawText;
}
