namespace CampaignManager.Web.Utilities.Services;

/// <summary>
///     Ключи <see cref="Model.UserPreferences" />-хранилища. Значения читают разные компоненты,
///     а меняет их личный кабинет, поэтому имена ключей держим в одном месте.
/// </summary>
public static class UserPreferenceKeys
{
    /// <summary>Боковое меню развёрнуто по умолчанию.</summary>
    public const string SidebarExpanded = "ui.sidebarExpanded";

    /// <summary>Помнить последнего открытого сыщика на всех устройствах.</summary>
    public const string SyncLastCharacter = "ui.syncLastCharacter";

    /// <summary>Идентификатор последнего открытого листа — заполняется, только если включён <see cref="SyncLastCharacter" />.</summary>
    public const string LastCharacterId = "ui.lastCharacterId";

    /// <summary>Имя последнего открытого сыщика — для подписи вкладки в нижнем меню.</summary>
    public const string LastCharacterName = "ui.lastCharacterName";
}
