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
using TRPG.Application.LocationSimulation.Commands;
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
}

internal sealed class TestWorldClock(TrpgDbContext context) : IWorldClock
{
    private readonly Dictionary<Guid, GameInstant> _unpersistedTimes = [];

    public async Task<GameInstant> GetCurrent(
        Guid worldId,
        CancellationToken cancellationToken = default
    )
    {
        var gameTime = await context
            .Worlds.AsNoTracking()
            .Where(world => world.Id == worldId)
            .Select(world => (GameInstant?)world.GameTime)
            .SingleOrDefaultAsync(cancellationToken);

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

    public Task CheckpointActiveWorlds(CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}

internal sealed class TestGameClientEventSink : IGameClientEventSink
{
    public List<GameClientEvent> EnqueuedEvents { get; } = [];

    public void Enqueue(GameClientEvent gameEvent) => EnqueuedEvents.Add(gameEvent);
}
