namespace CampaignManager.UI.Shared;

/// <summary>Наибольшая ширина окна: 28, 36, 48 и 64rem. На телефоне окно всегда во всю ширину без полей по 1rem.</summary>
public enum ModalSize
{
    Small,
    Medium,
    Large,
    ExtraLarge,
}

/// <summary>Где окно: по центру или листом снизу (меню «Ещё» на телефоне).</summary>
public enum ModalPlacement
{
    Center,
    Sheet,
}
