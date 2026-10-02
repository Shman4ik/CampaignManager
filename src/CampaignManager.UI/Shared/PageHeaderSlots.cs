namespace CampaignManager.UI.Shared;

/// <summary>
/// Слоты шапки страницы для кнопок модулей. Модуль рисует свою кнопку где угодно в дереве (обычно
/// компонентом в <c>MainLayout</c>): <c>&lt;SectionContent SectionName="@PageHeaderSlots.KeeperScreen"&gt;</c>,
/// а <see cref="PageHeader"/> показывает её через <c>SectionOutlet</c>. У слота один владелец:
/// второй <c>SectionContent</c> с тем же именем заменит первый, поэтому новому модулю — свой слот.
/// </summary>
public static class PageHeaderSlots
{
    /// <summary>Кнопка панели плеера (Music, T2.8).</summary>
    public const string Music = "page-header-music";

    /// <summary>Кнопка ширмы Хранителя (KeeperScreen, T2.7).</summary>
    public const string KeeperScreen = "page-header-keeper-screen";
}
