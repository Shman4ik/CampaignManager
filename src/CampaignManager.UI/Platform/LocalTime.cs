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

    /// <summary>
    /// Сколько прошло — для «изменён 2 часа назад»: «только что», минуты, часы, «вчера», дни до недели, дальше — дата.
    /// <paramref name="now"/> — <c>TimeProvider.GetUtcNow()</c> страницы (в тестах — подставное время).
    /// </summary>
    public static string Ago(DateTimeOffset value, DateTimeOffset now)
    {
        var passed = now - value;
        if (passed < TimeSpan.FromMinutes(1))
            return "только что";
        if (passed < TimeSpan.FromHours(1))
            return Count((int)passed.TotalMinutes, "минуту", "минуты", "минут") + " назад";
        if (passed < TimeSpan.FromDays(1))
            return Count((int)passed.TotalHours, "час", "часа", "часов") + " назад";

        var days = now.ToLocalTime().Date.Subtract(value.ToLocalTime().Date).Days;
        return days switch
        {
            <= 1 => "вчера",
            < 7 => Count(days, "день", "дня", "дней") + " назад",
            _ => Date(value),
        };
    }

    /// <summary>«1 час», «2 часа», «5 часов», «21 час».</summary>
    private static string Count(int count, string one, string few, string many)
    {
        var tens = count % 100;
        var units = count % 10;
        var word = tens is >= 11 and <= 14 ? many : units switch
        {
            1 => one,
            >= 2 and <= 4 => few,
            _ => many,
        };
        return count == 1 ? word : $"{count} {word}";
    }

    /// <summary>Значение для <c>&lt;input type="datetime-local"&gt;</c>: момент в поясе браузера, без смещения.</summary>
    public static System.DateTime? ToInput(DateTimeOffset? value) =>
        value is { } moment ? System.DateTime.SpecifyKind(moment.ToLocalTime().DateTime, DateTimeKind.Unspecified) : null;

    /// <summary>
    /// Обратно из <c>datetime-local</c>: «настенное» время браузера — момент в UTC (так его хранит сервер). Смещение берётся
    /// на саму дату, поэтому игра после перехода на зимнее время не съезжает на час.
    /// </summary>
    public static DateTimeOffset? FromInput(System.DateTime? value) =>
        value is { } local
            ? new DateTimeOffset(System.DateTime.SpecifyKind(local, DateTimeKind.Unspecified), TimeZoneInfo.Local.GetUtcOffset(local)).ToUniversalTime()
            : null;
}
