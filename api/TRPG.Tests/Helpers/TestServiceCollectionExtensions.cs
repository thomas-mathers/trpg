using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TRPG.Application.Common.Clocks;
using TRPG.Application.Common.Events;
using TRPG.Application.Common.Exceptions;
using TRPG.Application.Configuration;
using TRPG.Application.QuestGeneration.Commands;
using TRPG.Application.WorldSimulation;
using TRPG.Combat.Tools;
using TRPG.Data;
using TRPG.Domain;
using TRPG.Extensions;
using TRPG.GameSessions.Commands;
using TRPG.NpcConversations.Tools;

namespace TRPG.Tests.Helpers;

internal static class TestServiceCollectionExtensions
{
    private static readonly IReadOnlyCollection<ServiceDescriptor> ApplicationServices =
        new ServiceCollection().AddTrpgApplicationServices().ToArray();

    public static IServiceCollection AddTrpgTestServices(
        this IServiceCollection services,
        TrpgDbContext context
    )
    {
        foreach (var descriptor in ApplicationServices)
        {
            services.Add(descriptor);
        }

        services.RemoveAll<IWorldClock>();

        return services
            .AddGameTool<StartFightTool>()
            .AddGameTool<StartConversationTool>()
            .AddGameTool<EndConversationTool>()
            .AddTransient<EndGameSessionCommandHandler>()
            .AddScoped<TestGameClientEventSink>()
            .AddScoped<IGameClientEventSink>(sp => sp.GetRequiredService<TestGameClientEventSink>())
            .AddSingleton(context)
            .AddSingleton<IWorldClock>(new TestWorldClock(context))
            .AddModuleDbContexts()
            .AddSingleton(typeof(ILogger<>), typeof(NullLogger<>))
            .AddSingleton(typeof(IOptionsSnapshot<>), typeof(DefaultOptionsSnapshot<>))
            // Movement now reaches an LLM-backed generator, so the container needs the same shape
            // production has or every test that moves a player fails resolving it.
            .AddKeyedSingleton<IChatClient>(LlmRoleKeys.WorldGeneration, new FakeChatClient())
            .AddKeyedSingleton<IChatClient>(LlmRoleKeys.Gameplay, new FakeChatClient())
            // Location catch-up now reaches SeedLlmQuestChainCommand, which needs a scheduler —
            // same reasoning as the two keyed IChatClient registrations above. Tests that care about
            // what got scheduled register their own RecordingQuestChainGenerationScheduler instance
            // after AddTrpgTestServices(...), which wins over this default.
            .AddSingleton<IQuestChainGenerationScheduler, RecordingQuestChainGenerationScheduler>();
    }

    // Background workflows create their own scopes, and disposing a scope would dispose the shared test context.
    public static IServiceCollection WithScopedDbContexts(
        this IServiceCollection services,
        string connectionString
    ) =>
        services
            .RemoveAll<TrpgDbContext>()
            .AddDbContext<TrpgDbContext>(options => options.UseNpgsql(connectionString));

    // Gameplay actions are mailbox requests, so something has to tick the runner for them to complete.
    public static IServiceCollection WithSimulationPump(
        this IServiceCollection services,
        string connectionString
    )
    {
        var clock = services
            .Single(descriptor => descriptor.ServiceType == typeof(IWorldClock))
            .ImplementationInstance;
        if (clock is TestWorldClock testClock)
        {
            testClock.ReadConnectionString = connectionString;
        }

        return services.WithScopedDbContexts(connectionString).AddSingleton<SimulationPump>();
    }
}

internal sealed class SimulationPump : IAsyncDisposable
{
    private readonly CancellationTokenSource _stop = new();
    private readonly Task _loop;

    public SimulationPump(WorldSimulationCoordinator coordinator) =>
        _loop = Task.Run(() => Pump(coordinator));

    public async ValueTask DisposeAsync()
    {
        await _stop.CancelAsync();
        await _loop;
        _stop.Dispose();
    }

    private async Task Pump(WorldSimulationCoordinator coordinator)
    {
        while (!_stop.IsCancellationRequested)
        {
            try
            {
                await coordinator.Tick(_stop.Token);
                await Task.Delay(TimeSpan.FromMilliseconds(10), _stop.Token);
            }
            catch (Exception)
            {
                // The coordinator logs its own failures; a pump that dies would hang every pending request.
            }
        }
    }
}

internal sealed class TestWorldClock(TrpgDbContext context) : IWorldClock
{
    private readonly Dictionary<Guid, GameInstant> _unpersistedTimes = [];

    // Set when a background pump reads the clock while the test still uses the shared context.
    public string? ReadConnectionString { get; set; }

    public async Task<GameInstant> GetCurrent(
        Guid worldId,
        CancellationToken cancellationToken = default
    )
    {
        var gameTime = await ReadPersistedGameTime(worldId, cancellationToken);

        if (gameTime != null)
        {
            return gameTime.Value;
        }

        var belongsToSyntheticWorld = context
            .ChangeTracker.Entries()
            .SelectMany(entry => entry.Properties)
            .Any(property =>
                property.Metadata.Name == "WorldId" && Equals(property.CurrentValue, worldId)
            );
        belongsToSyntheticWorld |= await context
            .GameSessions.AsNoTracking()
            .AnyAsync(session => session.WorldId == worldId, cancellationToken);
        if (belongsToSyntheticWorld)
        {
            return _unpersistedTimes.GetValueOrDefault(worldId, GameClock.Epoch);
        }

        throw new EntityNotFoundException("World", worldId);
    }

    private async Task<GameInstant?> ReadPersistedGameTime(
        Guid worldId,
        CancellationToken cancellationToken
    )
    {
        if (ReadConnectionString is null)
        {
            return await QueryGameTime(context, worldId, cancellationToken);
        }

        var options = new DbContextOptionsBuilder<TrpgDbContext>()
            .UseNpgsql(ReadConnectionString)
            .Options;
        await using var readContext = new TrpgDbContext(options);

        return await QueryGameTime(readContext, worldId, cancellationToken);
    }

    private static Task<GameInstant?> QueryGameTime(
        TrpgDbContext source,
        Guid worldId,
        CancellationToken cancellationToken
    ) =>
        source
            .Worlds.AsNoTracking()
            .Where(world => world.Id == worldId)
            .Select(world => (GameInstant?)world.GameTime)
            .SingleOrDefaultAsync(cancellationToken);

    public Task<GameInstant> ResumeWorld(
        Guid worldId,
        CancellationToken cancellationToken = default
    ) => GetCurrent(worldId, cancellationToken);

    public Task<GameInstant> PauseWorld(
        Guid worldId,
        CancellationToken cancellationToken = default
    ) => GetCurrent(worldId, cancellationToken);

    public async Task<GameInstant> Advance(
        Guid worldId,
        TimeSpan duration,
        CancellationToken cancellationToken = default
    )
    {
        var current = await GetCurrent(worldId, cancellationToken);
        var gameTime = current + duration;
        var affectedRows = await context
            .Worlds.Where(world => world.Id == worldId)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(world => world.GameTime, gameTime),
                cancellationToken
            );
        if (affectedRows == 0)
        {
            _unpersistedTimes[worldId] = gameTime;
        }

        return gameTime;
    }

    public Task<GameInstant> Checkpoint(
        Guid worldId,
        CancellationToken cancellationToken = default
    ) => GetCurrent(worldId, cancellationToken);

    public IReadOnlyCollection<Guid> GetActiveWorldIds() => [];

    public Task CheckpointActiveWorlds(CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}

internal sealed class TestGameClientEventSink : IGameClientEventSink
{
    public List<GameClientEvent> EnqueuedEvents { get; } = [];

    public void Enqueue(GameClientEvent gameEvent) => EnqueuedEvents.Add(gameEvent);
}

internal sealed class NoOpGameClientEventDispatcher : IGameClientEventDispatcher
{
    public Task<bool> FlushAsync(Guid worldId, CancellationToken cancellationToken = default) =>
        Task.FromResult(false);
}
