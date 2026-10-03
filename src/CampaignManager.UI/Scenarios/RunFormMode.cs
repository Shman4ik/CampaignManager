namespace CampaignManager.UI.Scenarios;

/// <summary>Что делает одна форма прохождения (<see cref="RunFormModal"/>).</summary>
public enum RunFormMode
{
    /// <summary>«Начать игру» — прохождение в кампании, которую ведёте.</summary>
    PlayInCampaign,

    /// <summary>«Объявить ваншот» — кампания, Хранитель и прохождение с открытой записью.</summary>
    OneShot,

    /// <summary>Правка: состояние, дата, анонс, запись.</summary>
    Edit,
}
