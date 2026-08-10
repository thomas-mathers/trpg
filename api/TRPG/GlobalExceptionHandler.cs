using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using TRPG.Application.Common.Exceptions;

namespace TRPG;

internal class GlobalExceptionHandler : IExceptionHandler
{
    public ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken
    )
    {
        if (exception is not EntityNotFoundException)
        {
            return ValueTask.FromResult(false);
        }

        httpContext.Response.StatusCode = StatusCodes.Status404NotFound;
        return ValueTask.FromResult(true);
    }
}
