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
            .AddTransient<StreamChatTurnHandler>()
            .AddTransient<StreamFleeTurnHandler>()
            .AddTransient<StreamHostileEncounterActionTurnHandler>()
            .AddTransient<StreamGuardEncounterActionTurnHandler>()
            .AddTransient<StreamTheftEncounterNarrationTurnHandler>()
            .AddTransient<StreamTheftEncounterActionTurnHandler>()
            .AddTransient<ResolveCombatActionHandler>()
            .AddTransient<GameTurnRunner>(serviceProvider => new GameTurnRunner(
                serviceProvider.GetRequiredService<StreamOpeningTurnHandler>(),
                serviceProvider.GetRequiredService<StreamChatTurnHandler>(),
                serviceProvider.GetRequiredService<StreamWaitTurnHandler>(),
                serviceProvider.GetRequiredService<StreamFleeTurnHandler>(),
                serviceProvider.GetRequiredService<StreamHostileEncounterActionTurnHandler>(),
                serviceProvider.GetRequiredService<StreamGuardEncounterActionTurnHandler>(),
                serviceProvider.GetRequiredService<StreamTheftEncounterNarrationTurnHandler>(),
                serviceProvider.GetRequiredService<StreamTheftEncounterActionTurnHandler>(),
                serviceProvider.GetRequiredService<ResolveCombatActionHandler>()
            ));
}
