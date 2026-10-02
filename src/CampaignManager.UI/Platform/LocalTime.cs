using System.Globalization;

namespace CampaignManager.UI.Platform;

/// <summary>
/// Время на страницах — в часовом поясе пользователя (решение владельца, T2.2). Сервер хранит и отдаёт
/// <see cref="DateTimeOffset"/> в UTC (<c>timestamptz</c>, в JSON — со смещением), а WebAssembly берёт
/// <see cref="TimeZoneInfo.Local"/> у браузера: <c>ToLocalTime()</c> переводит в его пояс. Культура — ru-RU
/// при любом языке браузера. Даты без времени (<see cref="DateOnly"/>, дата встречи за столом) пояса не
/// имеют и не переводятся.
/// </summary>
public static class LocalTime
{
    private static readonly CultureInfo Russian = CultureInfo.GetCultureInfo("ru-RU");

    /// <summary>«2 октября 2026».</summary>
    public static string Date(DateTimeOffset value) => value.ToLocalTime().ToString("d MMMM yyyy", Russian);

    /// <summary>«10 октября 2026, 19:00».</summary>
    public static string DateTime(DateTimeOffset value) => value.ToLocalTime().ToString("d MMMM yyyy, HH:mm", Russian);

    /// <summary>«02.10.2026» — плотные строки списков.</summary>
    public static string ShortDate(DateTimeOffset value) => value.ToLocalTime().ToString("dd.MM.yyyy", Russian);

    /// <summary>Дата без пояса: «2 октября 2026».</summary>
    public static string Date(DateOnly value) => value.ToString("d MMMM yyyy", Russian);
}
