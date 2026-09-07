namespace CampaignManager.Web.Components.Features.Chase.Model;

/// <summary>
/// Способ передвижения (стр. 141). У кого нет отдельной СКО для способа, движется
/// с половиной своей СКО; полёт доступен не всем.
/// </summary>
public enum MovementMode
{
    OnFoot,
    Swimming,
    Flying
}
