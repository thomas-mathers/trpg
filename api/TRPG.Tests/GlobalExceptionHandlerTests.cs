using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using TRPG.Application.Common.Exceptions;

namespace TRPG.Tests;

public sealed class GlobalExceptionHandlerTests
{
    [Fact]
    public async Task TryHandleAsync_WritesEveryValidationError_WhenInputIsInvalid()
    {
        var httpContext = new DefaultHttpContext();
        await using var responseBody = new MemoryStream();
        httpContext.Response.Body = responseBody;
        var exception = new InputValidationException([
            new ValidationResult("A destination is required.", ["DestinationName"]),
            new ValidationResult("The destination is too long.", ["DestinationName"]),
            new ValidationResult("A player is required.", ["PlayerId"]),
        ]);

        var handled = await new GlobalExceptionHandler().TryHandleAsync(
            httpContext,
            exception,
            TestContext.Current.CancellationToken
        );

        responseBody.Position = 0;
        using var document = await JsonDocument.ParseAsync(
            responseBody,
            cancellationToken: TestContext.Current.CancellationToken
        );

        Assert.True(handled);
        Assert.Equal(StatusCodes.Status400BadRequest, httpContext.Response.StatusCode);
        var errors = document.RootElement.GetProperty("errors");
        Assert.Equal(2, errors.GetProperty("DestinationName").GetArrayLength());
        Assert.Equal("A player is required.", errors.GetProperty("PlayerId")[0].GetString());
    }
}
