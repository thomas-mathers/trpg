using Microsoft.Extensions.AI;
using TRPG.Application.GameTurns;
using TRPG.Application.Worlds;
using TRPG.Tools;

namespace TRPG.Tests.Tools;

public sealed class WorldMutationGatedFunctionTests
{
    private readonly WorldMutationGate _gate = new();
    private readonly GameTurnContext _turnContext = new() { WorldId = Guid.NewGuid() };

    [Fact]
    public async Task InvokeAsync_ReturnsInnerResult_WhenWorldIsNotHeld()
    {
        // Arrange
        var function = MakeGatedFunction(() => "looked");

        // Act
        var result = await function.InvokeAsync(
            cancellationToken: TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal("looked", result?.ToString());
    }

    [Fact]
    public async Task InvokeAsync_WaitsForWorldRelease_WhenBackgroundPassHoldsWorld()
    {
        // Arrange
        var function = MakeGatedFunction(() => "looked");
        var lease = await _gate.Acquire(
            _turnContext.WorldId,
            TestContext.Current.CancellationToken
        );

        // Act
        var invocation = function
            .InvokeAsync(cancellationToken: TestContext.Current.CancellationToken)
            .AsTask();

        // Assert
        await Task.Delay(50, TestContext.Current.CancellationToken);
        Assert.False(invocation.IsCompleted);
        await lease.DisposeAsync();
        var result = await invocation;
        Assert.Equal("looked", result?.ToString());
    }

    [Fact]
    public async Task InvokeAsync_ReleasesWorld_WhenInnerFunctionThrows()
    {
        // Arrange
        var function = MakeGatedFunction(() =>
            throw new InvalidOperationException("The tool failed.")
        );
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await function.InvokeAsync(cancellationToken: TestContext.Current.CancellationToken)
        );

        // Act
        var acquiring = _gate.Acquire(_turnContext.WorldId, TestContext.Current.CancellationToken);

        // Assert
        await using var lease = await acquiring.WaitAsync(
            TimeSpan.FromSeconds(5),
            TestContext.Current.CancellationToken
        );
    }

    private WorldMutationGatedFunction MakeGatedFunction(Func<string> tool) =>
        new(AIFunctionFactory.Create(tool, "tool"), _turnContext, _gate);
}
