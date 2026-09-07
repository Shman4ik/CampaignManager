namespace CampaignManager.Web.Components.Features.Combat.Model;

/// <summary>
///     Сторона участника боя. Заменяет прежний флаг «игрок / существо»: союзный НПС теперь
///     стоит рядом с отрядом, а не в одной группе с монстрами (важно для правила шальной
///     пули, стр. 112).
/// </summary>
public enum CombatSide
{
    /// <summary>Отряд: персонажи игроков и союзные НПС.</summary>
    Party = 0,

    /// <summary>Противники: монстры и враждебные НПС.</summary>
    Enemy = 1,

    /// <summary>Никому не союзник — нейтральные НПС и случайные прохожие.</summary>
    Neutral = 2
}
