using Microsoft.AspNetCore.Diagnostics;

namespace CampaignManager.Server.Access;

/// <summary><see cref="AccessDeniedException"/> → ProblemDetails с 403 или 404 вместо 500.</summary>
public sealed class AccessExceptionHandler(IProblemDetailsService problemDetails) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not AccessDeniedException denied)
        {
            return false;
        }

        httpContext.Response.StatusCode = denied.StatusCode;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = { Status = denied.StatusCode, Title = denied.Message },
        });
    }
}
