namespace CampaignManager.Web.Components.Features.Weapons.Model;

/// <summary>
///     Структурированная стоимость, разобранная из текстового поля
///     <see cref="Weapon.Cost" /> (колонка «Стоимость» таблицы XVII: «1920-е / современность»).
///     Заполняется <c>WeaponStatsParser.ParseCost</c>; хранится в БД как JSONB.
/// </summary>
public sealed class WeaponCostInfo
{
    /// <summary>Цена в долларах 1920-х. Null — цены нет либо она не разобрана.</summary>
    public decimal? Cost1920 { get; set; }

    /// <summary>Современная цена в долларах. Null — цены нет либо она не разобрана.</summary>
    public decimal? CostModern { get; set; }

    /// <summary>Цена 1920-х указана приблизительно: «от $200», «$0,65 – 5,25».</summary>
    public bool IsApproximate1920 { get; set; }

    /// <summary>Современная цена указана приблизительно.</summary>
    public bool IsApproximateModern { get; set; }

    /// <summary>В 1920-е оружие не продавалось: «—», «Нет».</summary>
    public bool Unavailable1920 { get; set; }

    /// <summary>Сейчас оружие не продаётся или цены нет: «—», «Нет», «редкое».</summary>
    public bool UnavailableModern { get; set; }

    /// <summary>Оригинальная строка до разбора.</summary>
    public string RawText { get; set; } = string.Empty;

    /// <summary>Удалось ли разобрать текст.</summary>
    public bool IsParsed { get; set; }

    public override string ToString() => RawText;
}
