using TRPG.Application.Worlds;

namespace TRPG.Tests.Application.Worlds;

public sealed class WorldMutationGateTests
{
    private readonly WorldMutationGate _gate = new();

    [Fact]
    public async Task Acquire_WaitsForRelease_WhenSameWorldIsHeld()
    {
        // Arrange
        var worldId = Guid.NewGuid();
        var held = await _gate.Acquire(worldId, TestContext.Current.CancellationToken);

        // Act
        var waiting = _gate.Acquire(worldId, TestContext.Current.CancellationToken);

        // Assert
        await Task.Delay(50, TestContext.Current.CancellationToken);
        Assert.False(waiting.IsCompleted);
        await held.DisposeAsync();
        await using var acquired = await waiting;
    }

    [Fact]
    public async Task Acquire_Completes_WhenDifferentWorldIsHeld()
    {
        // Arrange
        await using var held = await _gate.Acquire(
            Guid.NewGuid(),
            TestContext.Current.CancellationToken
        );

        // Act
        var acquiring = _gate.Acquire(Guid.NewGuid(), TestContext.Current.CancellationToken);

        // Assert
        await using var acquired = await acquiring.WaitAsync(
            TimeSpan.FromSeconds(5),
            TestContext.Current.CancellationToken
        );
    }

    [Fact]
    public async Task Acquire_ThrowsOperationCanceled_WhenCancelledWhileWaiting()
    {
        // Arrange
        var worldId = Guid.NewGuid();
        await using var held = await _gate.Acquire(worldId, TestContext.Current.CancellationToken);
        using var cancellation = new CancellationTokenSource();
        var waiting = _gate.Acquire(worldId, cancellation.Token);

        // Act & Assert
        await cancellation.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => waiting);
    }

    [Fact]
    public async Task Dispose_ReleasesOnce_WhenLeaseIsDisposedTwice()
    {
        // Arrange
        var worldId = Guid.NewGuid();
        var first = await _gate.Acquire(worldId, TestContext.Current.CancellationToken);
        await first.DisposeAsync();
        await using var second = await _gate.Acquire(
            worldId,
            TestContext.Current.CancellationToken
        );

        // Act
        await first.DisposeAsync();

        // Assert
        var waiting = _gate.Acquire(worldId, TestContext.Current.CancellationToken);
        await Task.Delay(50, TestContext.Current.CancellationToken);
        Assert.False(waiting.IsCompleted);
    }
}
