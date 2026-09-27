using Microsoft.Extensions.AI;
using TRPG.Application.Common.Serialization;
using TRPG.Tools;

namespace TRPG.Tests.Tools;

public sealed class ToolResultOutcomeTests
{
    private sealed record SuccessfulToolResult(string Error, int Count);

    private static AIFunction CreateFunction(Delegate tool) =>
        AIFunctionFactory.Create(
            tool,
            new AIFunctionFactoryOptions { SerializerOptions = TrpgJsonOptions.Default }
        );

    [Fact]
    public async Task IsRejected_ReturnsTrue_WhenFactoryFunctionReturnsToolError()
    {
        // Arrange
        var function = CreateFunction(() => new ToolError("The door is locked."));
        var result = await function.InvokeAsync(
            cancellationToken: TestContext.Current.CancellationToken
        );

        // Act
        var rejected = ToolResultOutcome.IsRejected(result);

        // Assert
        Assert.True(rejected);
    }

    [Fact]
    public async Task IsRejected_ReturnsFalse_WhenFactoryFunctionReturnsSuccessfulResult()
    {
        // Arrange
        var function = CreateFunction(() => new SuccessfulToolResult("none", 2));
        var result = await function.InvokeAsync(
            cancellationToken: TestContext.Current.CancellationToken
        );

        // Act
        var rejected = ToolResultOutcome.IsRejected(result);

        // Assert
        Assert.False(rejected);
    }

    [Fact]
    public void IsRejected_ReturnsTrue_WhenResultIsUnserializedToolError()
    {
        // Act
        var rejected = ToolResultOutcome.IsRejected(new ToolError("No exit."));

        // Assert
        Assert.True(rejected);
    }
}
