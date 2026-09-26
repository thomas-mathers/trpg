using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Worlds;
using TRPG.Data;
using TRPG.Data.ModuleContexts;
using TRPG.Domain;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Worlds;

public sealed class WorldClockTests(DatabaseFixture db)
    : IAsyncLifetime,
        IClassFixture<DatabaseFixture>
{
    private readonly ManualTimeProvider _timeProvider = new(
        new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)
    );

    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private WorldClock _clock = null!;
    private World _world = null!;

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _world = Builders.MakeWorld(gameTime: GameClock.Epoch + TimeSpan.FromHours(2));
        _context.Worlds.Add(_world);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        _serviceProvider = new ServiceCollection()
            .AddDbContext<TrpgDbContext>(options => options.UseNpgsql(db.ConnectionString))
            .AddScoped<IWorldsDbContext>(provider => provider.GetRequiredService<TrpgDbContext>())
            .BuildServiceProvider();
        _clock = new WorldClock(
            _serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            _timeProvider
        );
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task GetCurrent_DoesNotAdvance_WhenWorldIsPaused()
    {
        _timeProvider.Advance(TimeSpan.FromHours(1));

        var gameTime = await _clock.GetCurrent(_world.Id, TestContext.Current.CancellationToken);

        Assert.Equal(GameClock.Epoch + TimeSpan.FromHours(2), gameTime);
    }

    [Fact]
    public async Task GetCurrent_AdvancesOneToOne_WhenWorldIsActive()
    {
        await _clock.ResumeWorld(_world.Id, TestContext.Current.CancellationToken);

        _timeProvider.Advance(TimeSpan.FromMinutes(90));
        var gameTime = await _clock.GetCurrent(_world.Id, TestContext.Current.CancellationToken);

        Assert.Equal(GameClock.Epoch + TimeSpan.FromHours(3.5), gameTime);
    }

    [Fact]
    public async Task ResumeWorld_DoesNotResetAnActiveAnchor()
    {
        await _clock.ResumeWorld(_world.Id, TestContext.Current.CancellationToken);
        _timeProvider.Advance(TimeSpan.FromMinutes(30));

        await _clock.ResumeWorld(_world.Id, TestContext.Current.CancellationToken);
        _timeProvider.Advance(TimeSpan.FromMinutes(30));
        var gameTime = await _clock.GetCurrent(_world.Id, TestContext.Current.CancellationToken);

        Assert.Equal(GameClock.Epoch + TimeSpan.FromHours(3), gameTime);
    }

    [Fact]
    public async Task PauseWorld_CheckpointsAndStopsTheClock()
    {
        await _clock.ResumeWorld(_world.Id, TestContext.Current.CancellationToken);
        _timeProvider.Advance(TimeSpan.FromMinutes(45));

        var pausedAt = await _clock.PauseWorld(_world.Id, TestContext.Current.CancellationToken);
        _timeProvider.Advance(TimeSpan.FromHours(4));
        var current = await _clock.GetCurrent(_world.Id, TestContext.Current.CancellationToken);

        Assert.Equal(GameClock.Epoch + TimeSpan.FromHours(2.75), pausedAt);
        Assert.Equal(pausedAt, current);
        Assert.Equal(pausedAt, await ReadPersistedGameTime());
    }

    [Fact]
    public async Task Advance_PreservesElapsedActiveTimeAndPersistsTheSkip()
    {
        await _clock.ResumeWorld(_world.Id, TestContext.Current.CancellationToken);
        _timeProvider.Advance(TimeSpan.FromMinutes(15));

        var advanced = await _clock.Advance(
            _world.Id,
            TimeSpan.FromHours(3),
            TestContext.Current.CancellationToken
        );
        _timeProvider.Advance(TimeSpan.FromMinutes(15));
        var current = await _clock.GetCurrent(_world.Id, TestContext.Current.CancellationToken);

        Assert.Equal(GameClock.Epoch + TimeSpan.FromHours(5.25), advanced);
        Assert.Equal(GameClock.Epoch + TimeSpan.FromHours(5.5), current);
        Assert.Equal(advanced, await ReadPersistedGameTime());
    }

    [Fact]
    public async Task CheckpointActiveWorlds_PersistsWithoutPausing()
    {
        await _clock.ResumeWorld(_world.Id, TestContext.Current.CancellationToken);
        _timeProvider.Advance(TimeSpan.FromMinutes(20));

        await _clock.CheckpointActiveWorlds(TestContext.Current.CancellationToken);
        var checkpoint = await ReadPersistedGameTime();
        _timeProvider.Advance(TimeSpan.FromMinutes(10));
        var current = await _clock.GetCurrent(_world.Id, TestContext.Current.CancellationToken);

        Assert.Equal(
            GameClock.Epoch + TimeSpan.FromHours(2) + TimeSpan.FromMinutes(20),
            checkpoint
        );
        Assert.Equal(checkpoint + TimeSpan.FromMinutes(10), current);
    }

    private async Task<GameInstant> ReadPersistedGameTime()
    {
        await using var verifyContext = db.CreateContext();
        return await verifyContext
            .Worlds.Where(world => world.Id == _world.Id)
            .Select(world => world.GameTime)
            .SingleAsync(TestContext.Current.CancellationToken);
    }

    private sealed class ManualTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        private DateTimeOffset _utcNow = utcNow;

        public override DateTimeOffset GetUtcNow() => _utcNow;

        public void Advance(TimeSpan duration) => _utcNow += duration;
    }
}
