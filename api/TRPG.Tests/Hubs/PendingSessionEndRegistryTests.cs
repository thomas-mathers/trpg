using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TRPG.Application.Common.Clocks;
using TRPG.Application.Configuration;
using TRPG.Data;
using TRPG.Domain;
using TRPG.Domain.Models;
using TRPG.GameSessions.Hubs;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Hubs;

public sealed class PendingSessionEndRegistryTests(DatabaseFixture db)
    : IAsyncLifetime,
        IClassFixture<DatabaseFixture>
{
    private static readonly TimeSpan GracePeriod = TimeSpan.FromMilliseconds(50);

    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private RecordingWorldClock _worldClock = null!;
    private PendingSessionEndRegistry _registry = null!;
    private World _world = null!;
    private GameSession _session = null!;

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .BuildServiceProvider();
        _worldClock = new RecordingWorldClock();
        _registry = new PendingSessionEndRegistry(
            _serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            _worldClock,
            TimeProvider.System,
            new StaticOptionsMonitor<GameSessionOptions>(
                new GameSessionOptions { SessionEndGracePeriod = GracePeriod }
            ),
            NullLogger<PendingSessionEndRegistry>.Instance
        );

        _world = Builders.MakeWorld();
        var player = Builders.MakeCreature(_world.Id);
        _world.PlayerId = player.Id;
        _session = Builders.MakeGameSession(_world.Id, player.Id);
        _context.Worlds.Add(_world);
        _context.Creatures.Add(player);
        _context.GameSessions.Add(_session);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _registry.DisposeAsync();
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Disconnect_KeepsWorldActive_WhenAnotherConnectionRemains()
    {
        await _registry.Connect(_session.Id, _world.Id, TestContext.Current.CancellationToken);
        await _registry.Connect(_session.Id, _world.Id, TestContext.Current.CancellationToken);

        await _registry.Disconnect(_session.Id);
        await Task.Delay(GracePeriod * 2, TestContext.Current.CancellationToken);

        Assert.Equal(2, _worldClock.ResumeCount);
        Assert.Equal(0, _worldClock.PauseCount);
        Assert.True(await SessionExists());
    }

    [Fact]
    public async Task Disconnect_PausesAndEndsSession_AfterLastConnectionGraceExpires()
    {
        await _registry.Connect(_session.Id, _world.Id, TestContext.Current.CancellationToken);

        await _registry.Disconnect(_session.Id);
        await WaitForSessionEnd();

        Assert.Equal(1, _worldClock.PauseCount);
    }

    [Fact]
    public async Task Connect_CancelsPendingEnd_WhenReconnectingWithinGrace()
    {
        await _registry.Connect(_session.Id, _world.Id, TestContext.Current.CancellationToken);
        await _registry.Disconnect(_session.Id);

        await _registry.Connect(_session.Id, _world.Id, TestContext.Current.CancellationToken);
        await Task.Delay(GracePeriod * 2, TestContext.Current.CancellationToken);

        Assert.Equal(0, _worldClock.PauseCount);
        Assert.True(await SessionExists());
    }

    private async Task WaitForSessionEnd()
    {
        var timeoutAt = DateTimeOffset.UtcNow + TimeSpan.FromSeconds(5);
        while (await SessionExists())
        {
            Assert.True(DateTimeOffset.UtcNow < timeoutAt, "Session did not end after grace.");
            await Task.Delay(TimeSpan.FromMilliseconds(20), TestContext.Current.CancellationToken);
        }
    }

    private async Task<bool> SessionExists()
    {
        await using var verifyContext = db.CreateContext();
        return await verifyContext.GameSessions.AnyAsync(
            session => session.Id == _session.Id,
            TestContext.Current.CancellationToken
        );
    }

    private sealed class RecordingWorldClock : IWorldClock
    {
        public int ResumeCount { get; private set; }
        public int PauseCount { get; private set; }

        public Task<GameInstant> GetCurrent(
            Guid worldId,
            CancellationToken cancellationToken = default
        ) => Task.FromResult(GameClock.Epoch);

        public Task<GameInstant> ResumeWorld(
            Guid worldId,
            CancellationToken cancellationToken = default
        )
        {
            ResumeCount++;
            return Task.FromResult(GameClock.Epoch);
        }

        public Task<GameInstant> PauseWorld(
            Guid worldId,
            CancellationToken cancellationToken = default
        )
        {
            PauseCount++;
            return Task.FromResult(GameClock.Epoch);
        }

        public Task<GameInstant> Advance(
            Guid worldId,
            TimeSpan duration,
            CancellationToken cancellationToken = default
        ) => Task.FromResult(GameClock.Epoch + duration);

        public Task<GameInstant> Checkpoint(
            Guid worldId,
            CancellationToken cancellationToken = default
        ) => Task.FromResult(GameClock.Epoch);

        public Task CheckpointActiveWorlds(CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class StaticOptionsMonitor<T>(T currentValue) : IOptionsMonitor<T>
    {
        public T CurrentValue { get; } = currentValue;

        public T Get(string? name) => CurrentValue;

        public IDisposable? OnChange(Action<T, string?> listener) => null;
    }
}
