namespace CampaignManager.Web.Components.Features.Bestiary.Model;

/// <summary>
///     Строка «Навыки» из статблока существа: «Скрытность 90%», «Внимание (эхолокация) 75%».
///     Книга указывает навыки не у всех тварей — Хранитель волен добавить недостающие,
///     взяв за основу похожее существо (стр. 278).
/// </summary>
public class CreatureSkill
{
    /// <summary>Название навыка так, как его печатает книга.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Значение навыка в процентах.</summary>
    public int Value { get; set; }

    /// <summary>
    ///     Уточнение из скобок, если оно меняет значение по обстоятельствам:
    ///     «в воде 80%», «в лесу бонусная кость».
    /// </summary>
    public string? Note { get; set; }
}
