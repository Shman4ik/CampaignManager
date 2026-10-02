using System.Net;

namespace CampaignManager.Contracts.Platform;

/// <summary>
/// Машинный код отказа в ProblemDetails (расширение <c>code</c>): по нему клиент решает, что
/// предложить — «перечитать» при <see cref="Stale"/>, исправить имя при <see cref="Duplicate"/>.
/// Текст для человека — в <c>detail</c>.
/// </summary>
public static class ApiProblemCodes
{
    /// <summary>Расширение ProblemDetails с кодом.</summary>
    public const string Extension = "code";

    /// <summary>409: запись изменили на другом устройстве (устаревший <c>If-Match</c>).</summary>
    public const string Stale = "stale";

    /// <summary>409: имя или код уже заняты другой записью.</summary>
    public const string Duplicate = "duplicate";

    /// <summary>409: запись нельзя удалить — на неё ссылаются.</summary>
    public const string InUse = "in-use";

    /// <summary>
    /// 409: так нельзя по смыслу — убрать Хранителя, второй раз вступить, заявка уже на рассмотрении или уже
    /// рассмотрена, последний администратор. Предложить нечего, кроме текста.
    /// </summary>
    public const string Conflict = "conflict";

    /// <summary>400: данные не прошли проверку.</summary>
    public const string Invalid = "invalid";

    /// <summary>428: запись без <c>If-Match</c>.</summary>
    public const string VersionRequired = "version-required";
}

/// <summary>
/// Отказ API, прочитанный клиентом: статус, текст для человека и <see cref="ApiProblemCodes">код</see>.
/// Наследует <see cref="HttpRequestException"/>, поэтому общий разбор ошибок загрузки его тоже ловит.
/// </summary>
public sealed class ApiException(string message, HttpStatusCode statusCode, string? code)
    : HttpRequestException(message, null, statusCode)
{
    public string? Code { get; } = code;

    public bool IsStale => Code == ApiProblemCodes.Stale;
}
