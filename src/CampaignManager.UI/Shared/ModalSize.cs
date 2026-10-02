namespace CampaignManager.UI.Shared;

/// <summary>Наибольшая ширина окна: 28, 36, 48 и 64rem. На телефоне окно всегда во всю ширину без полей по 1rem.</summary>
public enum ModalSize
{
    Small,
    Medium,
    Large,
    ExtraLarge,
}

/// <summary>Где окно: по центру, листом снизу (меню «Ещё» на телефоне) или панелью справа (ширма Хранителя).</summary>
public enum ModalPlacement
{
    Center,
    Sheet,

    /// <summary>Панель во всю высоту справа: справочник поверх экрана, страница под ним не уходит.</summary>
    Drawer,

    /// <summary>
    /// Весь экран на тёмном фоне: раздатка, показанная игрокам (iPad развёрнут к столу — на экране только она). Шапка окна
    /// с крестиком остаётся: Хранителю нужно закрыть показ одним касанием.
    /// </summary>
    Showcase,
}
