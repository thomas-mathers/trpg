using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TRPG.Application.Common.Events;
using TRPG.Combat.Tools;
using TRPG.Data;
using TRPG.Extensions;
using TRPG.GameSessions.Commands;
using TRPG.Quests.Tools;

namespace TRPG.Tests.Helpers;

internal static class TestServiceCollectionExtensions
{
    public static IServiceCollection AddTrpgTestServices(
        this IServiceCollection services,
        TrpgDbContext context
    ) =>
        services
            .AddTrpgApplicationServices()
            .AddGameTool<StartFightTool>()
            .AddGameTool<ShowQuestDetailsTool>()
            .AddTransient<EndGameSessionCommandHandler>()
            .AddScoped<TestGameClientEventSink>()
            .AddScoped<IGameClientEventSink>(sp => sp.GetRequiredService<TestGameClientEventSink>())
            .AddSingleton(context)
            .AddModuleDbContexts()
            .AddSingleton(typeof(ILogger<>), typeof(NullLogger<>))
            .AddSingleton(typeof(IOptionsSnapshot<>), typeof(DefaultOptionsSnapshot<>));
}

internal sealed class TestGameClientEventSink : IGameClientEventSink
{
    public List<GameClientEvent> EnqueuedEvents { get; } = [];

    public void Enqueue(GameClientEvent gameEvent) => EnqueuedEvents.Add(gameEvent);
}
