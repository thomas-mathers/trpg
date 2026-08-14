using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using TRPG.Application.Common.Exceptions;

namespace TRPG;

internal class GlobalExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken
    )
    {
        switch (exception)
        {
            case EntityNotFoundException:
                httpContext.Response.StatusCode = StatusCodes.Status404NotFound;
                return true;
            case InvalidOperationException invalidOperationException:
                httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
                await httpContext.Response.WriteAsJsonAsync(
                    new ProblemDetails
                    {
                        Status = StatusCodes.Status400BadRequest,
                        Title = invalidOperationException.Message,
                    },
                    cancellationToken
                );
                return true;
            default:
                return false;
        }
    }
}
