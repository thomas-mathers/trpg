using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Common.Exceptions;
using TRPG.Application.Worlds;
using TRPG.Application.Worlds.Commands;
using TRPG.Data;
using TRPG.Domain;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Worlds.Commands;

public sealed class StampWorldStateCommandTests(DatabaseFixture db)
    : IAsyncLifetime,
        IClassFixture<DatabaseFixture>
{
    private readonly ManualTimeProvider _timeProvider = new(
        new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)
    );
    private readonly World _world = Builders.MakeWorld(
        gameTime: GameClock.Epoch + TimeSpan.FromHours(2)
    );
    private readonly List<ServiceProvider> _serviceProviders = [];
    private readonly List<TrpgDbContext> _contexts = [];

    private StampWorldStateCommandHandler _handler = null!;

    public async ValueTask InitializeAsync()
    {
        await using var seedContext = db.CreateContext();
        seedContext.Worlds.Add(_world);
        await seedContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        _handler = CreateHandler();
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var serviceProvider in _serviceProviders)
        {
            await serviceProvider.DisposeAsync();
        }

        foreach (var context in _contexts)
        {
            await context.DisposeAsync();
        }
    }

    private StampWorldStateCommandHandler CreateHandler()
    {
        var context = db.CreateContext();
        var serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(context)
            .AddSingleton<TimeProvider>(_timeProvider)
            .BuildServiceProvider();
        _contexts.Add(context);
        _serviceProviders.Add(serviceProvider);

        return serviceProvider.GetRequiredService<StampWorldStateCommandHandler>();
    }

    [Fact]
    public async Task Handle_ReturnsFirstVersion_WhenWorldHasNeverBeenStamped()
    {
        // Act
        var stamp = await _handler.Handle(
            new StampWorldStateCommand { WorldId = _world.Id },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(1, stamp.Version);
    }

    [Fact]
    public async Task Handle_ReturnsIncreasingVersionsAndPersistsThem_WhenStampedRepeatedly()
    {
        // Arrange
        var first = await _handler.Handle(
            new StampWorldStateCommand { WorldId = _world.Id },
            TestContext.Current.CancellationToken
        );

        // Act
        var second = await _handler.Handle(
            new StampWorldStateCommand { WorldId = _world.Id },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(first.Version + 1, second.Version);
        await using var verifyContext = db.CreateContext();
        var persisted = await verifyContext
            .Worlds.Where(world => world.Id == _world.Id)
            .Select(world => world.StateVersion)
            .SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(second.Version, persisted);
    }

    [Fact]
    public async Task Handle_ReturnsClockAnchor_WhenWorldIsPaused()
    {
        // Act
        var stamp = await _handler.Handle(
            new StampWorldStateCommand { WorldId = _world.Id },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(GameClock.Epoch + TimeSpan.FromHours(2), stamp.GameTime);
        Assert.Equal(_timeProvider.GetUtcNow(), stamp.CapturedAt);
    }

    [Fact]
    public async Task Handle_ReturnsUniqueVersions_WhenStampedConcurrently()
    {
        // Arrange
        var handlers = Enumerable.Range(0, 8).Select(_ => CreateHandler()).ToArray();

        // Act
        var stamps = await Task.WhenAll(
            handlers.Select(handler =>
                handler.Handle(
                    new StampWorldStateCommand { WorldId = _world.Id },
                    TestContext.Current.CancellationToken
                )
            )
        );

        // Assert
        Assert.Equal(
            Enumerable.Range(1, 8).Select(version => (long)version),
            stamps.Select(stamp => stamp.Version).Order()
        );
    }

    [Fact]
    public async Task Handle_Throws_WhenWorldDoesNotExist()
    {
        // Act & Assert
        await Assert.ThrowsAsync<EntityNotFoundException>(() =>
            _handler.Handle(
                new StampWorldStateCommand { WorldId = Guid.NewGuid() },
                TestContext.Current.CancellationToken
            )
        );
    }
}
