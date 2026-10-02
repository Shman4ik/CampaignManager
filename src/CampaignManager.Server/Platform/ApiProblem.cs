using CampaignManager.Contracts.Platform;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.EntityFrameworkCore;

namespace CampaignManager.Server.Platform;

/// <summary>
/// Отказ прикладного сервиса с понятным текстом: 400 (данные), 409 (дубль, используется, устарело),
/// 428 (нет <c>If-Match</c>). Код (<see cref="ApiProblemCodes"/>) уходит расширением ProblemDetails —
/// по нему клиент решает, что предложить. В v1 при дубле имени модалка молча закрывалась.
/// </summary>
public sealed class ApiProblemException(int statusCode, string code, string message) : Exception(message)
{
    public int StatusCode { get; } = statusCode;

    public string Code { get; } = code;

    public static ApiProblemException Invalid(string message) =>
        new(StatusCodes.Status400BadRequest, ApiProblemCodes.Invalid, message);

    /// <summary>409 «так нельзя по смыслу»: состояние не то (заявка уже рассмотрена, последний администратор).</summary>
    public static ApiProblemException Conflict(string message) =>
        new(StatusCodes.Status409Conflict, ApiProblemCodes.Conflict, message);

    public static ApiProblemException Duplicate(string message) =>
        new(StatusCodes.Status409Conflict, ApiProblemCodes.Duplicate, message);

    public static ApiProblemException InUse(string message) =>
        new(StatusCodes.Status409Conflict, ApiProblemCodes.InUse, message);

    public static ApiProblemException Stale() =>
        new(StatusCodes.Status409Conflict, ApiProblemCodes.Stale, StaleMessage);

    public static ApiProblemException VersionRequired() =>
        new(StatusCodes.Status428PreconditionRequired, ApiProblemCodes.VersionRequired,
            "Правка без версии записи: пришлите If-Match с версией, которую правили.");

    internal const string StaleMessage = "Запись изменили на другом устройстве. Перечитайте её и повторите правку.";
}

/// <summary>
/// <see cref="ApiProblemException"/> и устаревшая версия строки (<see cref="DbUpdateConcurrencyException"/>,
/// <c>xmin</c>) → ProblemDetails с кодом вместо 500.
/// </summary>
public sealed class ApiProblemExceptionHandler(IProblemDetailsService problemDetails) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var (status, code, message) = exception switch
        {
            ApiProblemException problem => (problem.StatusCode, problem.Code, problem.Message),
            DbUpdateConcurrencyException => (StatusCodes.Status409Conflict, ApiProblemCodes.Stale, ApiProblemException.StaleMessage),
            _ => (0, "", ""),
        };
        if (status == 0)
        {
            return false;
        }

        httpContext.Response.StatusCode = status;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails =
            {
                Status = status,
                Title = message,
                Detail = message,
                Extensions = { [ApiProblemCodes.Extension] = code },
            },
        });
    }
}
