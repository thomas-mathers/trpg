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
            .AddTransient<StreamSitDownTurnHandler>()
            .AddTransient<StreamStandUpTurnHandler>()
            .AddTransient<StreamSleepTurnHandler>()
            .AddTransient<StreamActivateTriggerTurnHandler>()
            .AddTransient<StreamAcceptQuestTurnHandler>()
            .AddTransient<StreamDeclineQuestTurnHandler>()
            .AddTransient<StreamCompleteQuestTurnHandler>()
            .AddTransient<StreamDeliverItemTurnHandler>()
            .AddTransient<StreamChatTurnHandler>()
            .AddTransient<StreamFleeTurnHandler>()
            .AddTransient<StreamRespawnTurnHandler>()
            .AddTransient<StreamHostileEncounterActionTurnHandler>()
            .AddTransient<StreamShakedownEncounterActionTurnHandler>()
            .AddTransient<StreamGuardEncounterActionTurnHandler>()
            .AddTransient<StreamSuspicionEncounterActionTurnHandler>()
            .AddTransient<StreamTrapEncounterActionTurnHandler>()
            .AddTransient<StreamTheftEncounterNarrationTurnHandler>()
            .AddTransient<StreamTheftEncounterActionTurnHandler>()
            .AddTransient<StreamCombatActionTurnHandler>()
            .AddTransient<StreamPurchaseCaravanTicketTurnHandler>()
            .AddTransient<StreamDeclineCaravanTicketTurnHandler>()
            .AddTransient<StreamBoardCaravanTurnHandler>()
            .AddTransient<GameTurnRunner>(serviceProvider => new GameTurnRunner(
                serviceProvider.GetRequiredService<StreamOpeningTurnHandler>(),
                serviceProvider.GetRequiredService<StreamChatTurnHandler>(),
                serviceProvider.GetRequiredService<StreamWaitTurnHandler>(),
                serviceProvider.GetRequiredService<StreamSitDownTurnHandler>(),
                serviceProvider.GetRequiredService<StreamStandUpTurnHandler>(),
                serviceProvider.GetRequiredService<StreamSleepTurnHandler>(),
                serviceProvider.GetRequiredService<StreamActivateTriggerTurnHandler>(),
                serviceProvider.GetRequiredService<StreamAcceptQuestTurnHandler>(),
                serviceProvider.GetRequiredService<StreamDeclineQuestTurnHandler>(),
                serviceProvider.GetRequiredService<StreamCompleteQuestTurnHandler>(),
                serviceProvider.GetRequiredService<StreamDeliverItemTurnHandler>(),
                serviceProvider.GetRequiredService<StreamFleeTurnHandler>(),
                serviceProvider.GetRequiredService<StreamRespawnTurnHandler>(),
                serviceProvider.GetRequiredService<StreamHostileEncounterActionTurnHandler>(),
                serviceProvider.GetRequiredService<StreamShakedownEncounterActionTurnHandler>(),
                serviceProvider.GetRequiredService<StreamGuardEncounterActionTurnHandler>(),
                serviceProvider.GetRequiredService<StreamSuspicionEncounterActionTurnHandler>(),
                serviceProvider.GetRequiredService<StreamTrapEncounterActionTurnHandler>(),
                serviceProvider.GetRequiredService<StreamTheftEncounterNarrationTurnHandler>(),
                serviceProvider.GetRequiredService<StreamTheftEncounterActionTurnHandler>(),
                serviceProvider.GetRequiredService<StreamCombatActionTurnHandler>(),
                serviceProvider.GetRequiredService<StreamPurchaseCaravanTicketTurnHandler>(),
                serviceProvider.GetRequiredService<StreamDeclineCaravanTicketTurnHandler>(),
                serviceProvider.GetRequiredService<StreamBoardCaravanTurnHandler>()
            ));
}
