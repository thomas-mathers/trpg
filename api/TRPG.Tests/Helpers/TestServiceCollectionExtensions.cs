using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TRPG.Application.Common.Events;
using TRPG.Application.Configuration;
using TRPG.Application.LocationSimulation.Commands;
using TRPG.Combat.Tools;
using TRPG.Data;
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

        return services
            .AddGameTool<StartFightTool>()
            .AddGameTool<StartConversationTool>()
            .AddTransient<EndGameSessionCommandHandler>()
            .AddScoped<TestGameClientEventSink>()
            .AddScoped<IGameClientEventSink>(sp => sp.GetRequiredService<TestGameClientEventSink>())
            .AddSingleton(context)
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

internal sealed class TestGameClientEventSink : IGameClientEventSink
{
    public List<GameClientEvent> EnqueuedEvents { get; } = [];

    public void Enqueue(GameClientEvent gameEvent) => EnqueuedEvents.Add(gameEvent);
}
