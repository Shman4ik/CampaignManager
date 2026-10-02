using Microsoft.AspNetCore.Diagnostics;

namespace CampaignManager.Server.Campaigns;

/// <summary>
/// Запрос нельзя выполнить по смыслу, а не по правам: пустое название, чужое прохождение во встрече
/// (400), второй раз вступить или убрать Хранителя (409). Текст уходит пользователю как есть.
/// </summary>
public sealed class CampaignRejectedException(string message, int statusCode = StatusCodes.Status400BadRequest) : Exception(message)
{
    public int StatusCode { get; } = statusCode;

    public static CampaignRejectedException Conflict(string message) => new(message, StatusCodes.Status409Conflict);
}

/// <summary><see cref="CampaignRejectedException"/> → ProblemDetails с текстом в <c>detail</c>.</summary>
public sealed class CampaignRejectedExceptionHandler(IProblemDetailsService problemDetails) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not CampaignRejectedException rejected)
        {
            return false;
        }

        httpContext.Response.StatusCode = rejected.StatusCode;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = { Status = rejected.StatusCode, Detail = rejected.Message },
        });
    }
}
