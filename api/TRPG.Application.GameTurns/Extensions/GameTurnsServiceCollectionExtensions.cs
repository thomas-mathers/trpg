using Microsoft.Extensions.DependencyInjection;

namespace TRPG.Application.GameTurns.Extensions;

public static class GameTurnsServiceCollectionExtensions
{
    public static IServiceCollection AddGameTurnsServices(
        this IServiceCollection serviceCollection
    ) =>
        serviceCollection
            .AddTransient<LlmConversationClient>()
            .AddTransient<TurnSceneDiffer>()
            .AddTransient<GameTurnStreamer>()
            .AddTransient<GameActionRunner>()
            .AddTransient<StreamChatTurnHandler>()
            .AddTransient<PlayerRespawner>()
            .AddTransient<CastAbilityTurnResolver>()
            .AddTransient<MoveTurnResolver>()
            .AddTransient<WaitActionHandler>()
            .AddTransient<SitDownActionHandler>()
            .AddTransient<StandUpActionHandler>()
            .AddTransient<SleepActionHandler>()
            .AddTransient<ActivateTriggerActionHandler>()
            .AddTransient<AcceptQuestActionHandler>()
            .AddTransient<CompleteQuestActionHandler>()
            .AddTransient<DeliverItemActionHandler>()
            .AddTransient<FleeActionHandler>()
            .AddTransient<RespawnActionHandler>()
            .AddTransient<HostileEncounterActionHandler>()
            .AddTransient<ShakedownEncounterActionHandler>()
            .AddTransient<GuardEncounterActionHandler>()
            .AddTransient<SuspicionEncounterActionHandler>()
            .AddTransient<TrapEncounterActionHandler>()
            .AddTransient<StartTheftEncounterActionHandler>()
            .AddTransient<TheftEncounterActionHandler>()
            .AddTransient<CombatActionHandler>()
            .AddTransient<CastAbilityActionHandler>()
            .AddTransient<MoveActionHandler>()
            .AddTransient<GameTurnRunner>(serviceProvider => new GameTurnRunner(
                serviceProvider.GetRequiredService<StreamChatTurnHandler>(),
                serviceProvider.GetRequiredService<WaitActionHandler>(),
                serviceProvider.GetRequiredService<SitDownActionHandler>(),
                serviceProvider.GetRequiredService<StandUpActionHandler>(),
                serviceProvider.GetRequiredService<SleepActionHandler>(),
                serviceProvider.GetRequiredService<ActivateTriggerActionHandler>(),
                serviceProvider.GetRequiredService<AcceptQuestActionHandler>(),
                serviceProvider.GetRequiredService<CompleteQuestActionHandler>(),
                serviceProvider.GetRequiredService<DeliverItemActionHandler>(),
                serviceProvider.GetRequiredService<FleeActionHandler>(),
                serviceProvider.GetRequiredService<RespawnActionHandler>(),
                serviceProvider.GetRequiredService<HostileEncounterActionHandler>(),
                serviceProvider.GetRequiredService<ShakedownEncounterActionHandler>(),
                serviceProvider.GetRequiredService<GuardEncounterActionHandler>(),
                serviceProvider.GetRequiredService<SuspicionEncounterActionHandler>(),
                serviceProvider.GetRequiredService<TrapEncounterActionHandler>(),
                serviceProvider.GetRequiredService<StartTheftEncounterActionHandler>(),
                serviceProvider.GetRequiredService<TheftEncounterActionHandler>(),
                serviceProvider.GetRequiredService<CombatActionHandler>(),
                serviceProvider.GetRequiredService<CastAbilityActionHandler>(),
                serviceProvider.GetRequiredService<MoveActionHandler>()
            ));
}
