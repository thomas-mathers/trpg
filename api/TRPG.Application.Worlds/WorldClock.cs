using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Common.Clocks;
using TRPG.Application.Common.Exceptions;
using TRPG.Data.ModuleContexts;
using TRPG.Domain;

namespace TRPG.Application.Worlds;

internal sealed class WorldClock(
    IServiceScopeFactory serviceScopeFactory,
    TimeProvider timeProvider
) : IWorldClock
{
    private readonly ConcurrentDictionary<Guid, WorldClockState> _states = new();

    public async Task<GameInstant> GetCurrent(
        Guid worldId,
        CancellationToken cancellationToken = default
    )
    {
        var state = GetState(worldId);
        await state.Gate.WaitAsync(cancellationToken);
        try
        {
            return state.Anchor is { } anchor
                ? GetCurrent(anchor)
                : await ReadPersisted(worldId, cancellationToken);
        }
        finally
        {
            state.Gate.Release();
        }
    }

    public async Task<GameInstant> ResumeWorld(
        Guid worldId,
        CancellationToken cancellationToken = default
    )
    {
        var state = GetState(worldId);
        await state.Gate.WaitAsync(cancellationToken);
        try
        {
            if (state.Anchor is { } activeAnchor)
            {
                return GetCurrent(activeAnchor);
            }

            var gameTime = await ReadPersisted(worldId, cancellationToken);
            state.Anchor = new WorldClockAnchor(gameTime, GetUtcNow());
            return gameTime;
        }
        finally
        {
            state.Gate.Release();
        }
    }

    public async Task<GameInstant> PauseWorld(
        Guid worldId,
        CancellationToken cancellationToken = default
    )
    {
        var state = GetState(worldId);
        await state.Gate.WaitAsync(cancellationToken);
        try
        {
            if (state.Anchor is not { } anchor)
            {
                return await ReadPersisted(worldId, cancellationToken);
            }

            var gameTime = GetCurrent(anchor);
            await Persist(worldId, gameTime, cancellationToken);
            state.Anchor = null;
            return gameTime;
        }
        finally
        {
            state.Gate.Release();
        }
    }

    public async Task<GameInstant> Advance(
        Guid worldId,
        TimeSpan duration,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(duration, TimeSpan.Zero);

        var state = GetState(worldId);
        await state.Gate.WaitAsync(cancellationToken);
        try
        {
            var now = GetUtcNow();
            var current = state.Anchor is { } anchor
                ? GetCurrent(anchor, now)
                : await ReadPersisted(worldId, cancellationToken);
            var advanced = current + duration;

            await Persist(worldId, advanced, cancellationToken);
            if (state.Anchor != null)
            {
                state.Anchor = new WorldClockAnchor(advanced, now);
            }

            return advanced;
        }
        finally
        {
            state.Gate.Release();
        }
    }

    public async Task<GameInstant> Checkpoint(
        Guid worldId,
        CancellationToken cancellationToken = default
    )
    {
        var state = GetState(worldId);
        await state.Gate.WaitAsync(cancellationToken);
        try
        {
            if (state.Anchor is not { } anchor)
            {
                return await ReadPersisted(worldId, cancellationToken);
            }

            var now = GetUtcNow();
            var gameTime = GetCurrent(anchor, now);
            await Persist(worldId, gameTime, cancellationToken);
            state.Anchor = new WorldClockAnchor(gameTime, now);
            return gameTime;
        }
        finally
        {
            state.Gate.Release();
        }
    }

    public IReadOnlyCollection<Guid> GetActiveWorldIds() =>
        _states.Where(pair => pair.Value.Anchor != null).Select(pair => pair.Key).ToArray();

    public async Task CheckpointActiveWorlds(CancellationToken cancellationToken = default)
    {
        foreach (var worldId in GetActiveWorldIds())
        {
            await Checkpoint(worldId, cancellationToken);
        }
    }

    private WorldClockState GetState(Guid worldId) =>
        _states.GetOrAdd(worldId, static _ => new WorldClockState());

    private DateTimeOffset GetUtcNow() => timeProvider.GetUtcNow().ToUniversalTime();

    private GameInstant GetCurrent(WorldClockAnchor anchor) => GetCurrent(anchor, GetUtcNow());

    private static GameInstant GetCurrent(WorldClockAnchor anchor, DateTimeOffset now) =>
        anchor.GameTime + (now - anchor.RealTime);

    private async Task<GameInstant> ReadPersisted(Guid worldId, CancellationToken cancellationToken)
    {
        await using var scope = serviceScopeFactory.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<IWorldsDbContext>();
        var gameTime = await context
            .Worlds.AsNoTracking()
            .Where(world => world.Id == worldId)
            .Select(world => (GameInstant?)world.GameTime)
            .SingleOrDefaultAsync(cancellationToken);

        if (gameTime == null)
        {
            throw new EntityNotFoundException("World", worldId);
        }

        return gameTime.Value;
    }

    private async Task Persist(
        Guid worldId,
        GameInstant gameTime,
        CancellationToken cancellationToken
    )
    {
        await using var scope = serviceScopeFactory.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<IWorldsDbContext>();
        var updated = await context
            .Worlds.Where(world => world.Id == worldId)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(world => world.GameTime, gameTime),
                cancellationToken
            );

        if (updated == 0)
        {
            throw new EntityNotFoundException("World", worldId);
        }
    }

    private sealed class WorldClockState
    {
        public SemaphoreSlim Gate { get; } = new(1, 1);
        public WorldClockAnchor? Anchor { get; set; }
    }

    private sealed record WorldClockAnchor(GameInstant GameTime, DateTimeOffset RealTime);
}
