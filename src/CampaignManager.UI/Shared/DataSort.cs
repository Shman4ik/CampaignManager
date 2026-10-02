namespace CampaignManager.UI.Shared;

/// <summary>Сортировка списка снаружи таблицы: ключ колонки и направление.</summary>
public sealed record DataSort(string Key, bool Descending);
