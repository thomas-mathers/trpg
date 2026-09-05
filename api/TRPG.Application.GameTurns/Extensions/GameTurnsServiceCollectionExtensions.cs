using Microsoft.Extensions.DependencyInjection;

namespace TRPG.Application.GameTurns.Extensions;

public static class GameTurnsServiceCollectionExtensions
{
    public static IServiceCollection AddGameTurnsServices(
        this IServiceCollection serviceCollection
    ) =>
        serviceCollection
            .AddTransient<LlmConversationClient>()
            .AddTransient<GameTurnStreamer>()
            .AddTransient<StreamOpeningTurnHandler>()
            .AddTransient<StreamWaitTurnHandler>()
            .AddTransient<StreamSleepTurnHandler>()
            .AddTransient<StreamChatTurnHandler>()
            .AddTransient<StreamFleeTurnHandler>()
            .AddTransient<StreamRespawnTurnHandler>()
            .AddTransient<StreamHostileEncounterActionTurnHandler>()
            .AddTransient<StreamGuardEncounterActionTurnHandler>()
            .AddTransient<StreamSuspicionEncounterActionTurnHandler>()
            .AddTransient<StreamTheftEncounterNarrationTurnHandler>()
            .AddTransient<StreamTheftEncounterActionTurnHandler>()
            .AddTransient<StreamCombatActionTurnHandler>()
            .AddTransient<GameTurnRunner>(serviceProvider => new GameTurnRunner(
                serviceProvider.GetRequiredService<StreamOpeningTurnHandler>(),
                serviceProvider.GetRequiredService<StreamChatTurnHandler>(),
                serviceProvider.GetRequiredService<StreamWaitTurnHandler>(),
                serviceProvider.GetRequiredService<StreamSleepTurnHandler>(),
                serviceProvider.GetRequiredService<StreamFleeTurnHandler>(),
                serviceProvider.GetRequiredService<StreamRespawnTurnHandler>(),
                serviceProvider.GetRequiredService<StreamHostileEncounterActionTurnHandler>(),
                serviceProvider.GetRequiredService<StreamGuardEncounterActionTurnHandler>(),
                serviceProvider.GetRequiredService<StreamSuspicionEncounterActionTurnHandler>(),
                serviceProvider.GetRequiredService<StreamTheftEncounterNarrationTurnHandler>(),
                serviceProvider.GetRequiredService<StreamTheftEncounterActionTurnHandler>(),
                serviceProvider.GetRequiredService<StreamCombatActionTurnHandler>()
            ));
}
