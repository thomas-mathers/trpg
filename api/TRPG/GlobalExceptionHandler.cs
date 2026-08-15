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
            case InputValidationException inputValidationException:
                httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
                await httpContext.Response.WriteAsJsonAsync(
                    new ValidationProblemDetails(ToValidationErrors(inputValidationException))
                    {
                        Status = StatusCodes.Status400BadRequest,
                    },
                    cancellationToken
                );
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

    private static Dictionary<string, string[]> ToValidationErrors(
        InputValidationException exception
    )
    {
        var messagesByMember = new Dictionary<string, List<string>>();
        foreach (var error in exception.Errors)
        {
            foreach (var memberName in error.MemberNames.DefaultIfEmpty(string.Empty))
            {
                if (!messagesByMember.TryGetValue(memberName, out var messages))
                {
                    messages = [];
                    messagesByMember[memberName] = messages;
                }

                messages.Add(error.ErrorMessage ?? "Invalid value.");
            }
        }

        return messagesByMember.ToDictionary(entry => entry.Key, entry => entry.Value.ToArray());
    }
}
