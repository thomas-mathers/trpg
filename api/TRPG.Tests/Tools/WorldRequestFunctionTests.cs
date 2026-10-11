using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.GameTurns;
using TRPG.Application.WorldSimulation;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;
using TRPG.Tools;

namespace TRPG.Tests.Tools;

public sealed class WorldRequestFunctionTests(DatabaseFixture db)
    : IAsyncLifetime,
        IClassFixture<DatabaseFixture>
{
    private readonly World _world = Builders.MakeWorld();

    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private WorldSimulationCoordinator _coordinator = null!;
    private GameTurnContext _turnContext = null!;

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .WithSimulationPump(db.ConnectionString)
            .BuildServiceProvider();
        _serviceProvider.GetRequiredService<SimulationPump>();
        _coordinator = _serviceProvider.GetRequiredService<WorldSimulationCoordinator>();
        _turnContext = new GameTurnContext { WorldId = _world.Id };

        _context.Worlds.Add(_world);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _context.DisposeAsync();
        await _serviceProvider.DisposeAsync();
    }

    [Fact]
    public async Task InvokeAsync_ReturnsInnerResult_WhenToolReturns()
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
    public async Task InvokeAsync_CountsTheInvocation_WhenToolReturns()
    {
        // Arrange
        var function = MakeGatedFunction(() => "looked");

        // Act
        await function.InvokeAsync(cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(1, _turnContext.ToolInvocations);
    }

    [Fact]
    public async Task InvokeAsync_WaitsForTheRunnersCurrentRequest_WhenOneIsInFlight()
    {
        // Arrange
        var function = MakeGatedFunction(() => "looked");
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var inFlight = _coordinator.Request(
            _world.Id,
            async (_, _) =>
            {
                started.SetResult();
                await release.Task;
            },
            TestContext.Current.CancellationToken
        );
        await started.Task.WaitAsync(
            TimeSpan.FromSeconds(5),
            TestContext.Current.CancellationToken
        );

        // Act
        var invocation = function
            .InvokeAsync(cancellationToken: TestContext.Current.CancellationToken)
            .AsTask();

        // Assert
        await Task.Delay(50, TestContext.Current.CancellationToken);
        Assert.False(invocation.IsCompleted);
        release.SetResult();
        await inFlight;
        var result = await invocation;
        Assert.Equal("looked", result?.ToString());
    }

    [Fact]
    public async Task InvokeAsync_KeepsTheRunnerUsable_WhenInnerFunctionThrows()
    {
        // Arrange
        var function = MakeGatedFunction(() =>
            throw new InvalidOperationException("The tool failed.")
        );
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await function.InvokeAsync(cancellationToken: TestContext.Current.CancellationToken)
        );

        // Act
        var request = _coordinator.Request(
            _world.Id,
            (_, _) => Task.FromResult("ran"),
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(
            "ran",
            await request.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken)
        );
    }

    private WorldRequestFunction MakeGatedFunction(Func<string> tool) =>
        new(AIFunctionFactory.Create(tool, "tool"), _turnContext, _coordinator);
}
