using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Common.Events;
using TRPG.Application.WorldSimulation.EventHandlers;
using TRPG.Application.WorldSimulation.LocalActivities;
using TRPG.Application.WorldSimulation.Publishing;
using TRPG.Application.WorldSimulation.Subscriptions;

namespace TRPG.Application.WorldSimulation.Extensions;

public static class WorldSimulationServiceCollectionExtensions
{
    public static IServiceCollection AddWorldSimulationServices(
        this IServiceCollection serviceCollection
    ) =>
        serviceCollection
            .AddTransient<CreatureVitalsPublisher>()
            .AddTransient<ScenePublisher>()
            .AddTransient<VitalsAnnouncer>()
            .AddTransient<ArrivalCatchUp>()
            .AddTransient<CreatureChangeAnnouncer>()
            .AddTransient<IActionSceneAnnouncer, ActionSceneAnnouncer>()
            .AddTransient<WeatherAnnouncer>()
            .AddTransient<PropOccupancyAnnouncer>()
            .AddTransient<PlayerMovedCorpseCleanupEventHandler>()
            .AddTransient<IDomainEventConsumer<PlayerMovedEvent>>(serviceProvider =>
                serviceProvider.GetRequiredService<PlayerMovedCorpseCleanupEventHandler>()
            )
            .AddTransient<PlayerMovedAlertedCreatureResetEventHandler>()
            .AddTransient<IDomainEventConsumer<PlayerMovedEvent>>(serviceProvider =>
                serviceProvider.GetRequiredService<PlayerMovedAlertedCreatureResetEventHandler>()
            )
            .AddTransient<PlayerMovedArrivalEventHandler>()
            .AddTransient<IDomainEventConsumer<PlayerMovedEvent>>(serviceProvider =>
                serviceProvider.GetRequiredService<PlayerMovedArrivalEventHandler>()
            )
            .AddTransient<PlayerMovedJoblessCaptiveCleanupEventHandler>()
            .AddTransient<IDomainEventConsumer<PlayerMovedEvent>>(serviceProvider =>
                serviceProvider.GetRequiredService<PlayerMovedJoblessCaptiveCleanupEventHandler>()
            )
            .AddTransient<PlayerMovedPoseSeedEventHandler>()
            .AddTransient<IDomainEventConsumer<PlayerMovedEvent>>(serviceProvider =>
                serviceProvider.GetRequiredService<PlayerMovedPoseSeedEventHandler>()
            )
            .AddTransient<PropAddedMovementSpaceEventHandler>()
            .AddTransient<IDomainEventConsumer<PropAddedEvent>>(serviceProvider =>
                serviceProvider.GetRequiredService<PropAddedMovementSpaceEventHandler>()
            )
            .AddScoped<WorldSimulatorLoader>()
            .AddScoped<WeatherExposureProvider>()
            .AddScoped<LocalActivityPlanner>()
            .AddScoped<LocalActivityCompleter>()
            .AddSingleton<WorldSimulationRunnerFactory>()
            .AddSingleton<SceneSubscriptions>()
            .AddSingleton<SceneEventProjector>()
            .AddSingleton<WorldSimulationCoordinator>()
            .AddTransient<CreaturesEngagedSimulationEventHandler>()
            .AddTransient<IDomainEventConsumer<CreaturesEngagedEvent>>(serviceProvider =>
                serviceProvider.GetRequiredService<CreaturesEngagedSimulationEventHandler>()
            )
            .AddTransient<CreatureFreedSimulationEventHandler>()
            .AddTransient<IDomainEventConsumer<CreatureFreedEvent>>(serviceProvider =>
                serviceProvider.GetRequiredService<CreatureFreedSimulationEventHandler>()
            )
            .AddTransient<CreaturesReleasedSimulationEventHandler>()
            .AddTransient<IDomainEventConsumer<CreaturesReleasedEvent>>(serviceProvider =>
                serviceProvider.GetRequiredService<CreaturesReleasedSimulationEventHandler>()
            )
            .AddTransient<CreaturesDiedSimulationEventHandler>()
            .AddTransient<IDomainEventConsumer<CreaturesDiedEvent>>(serviceProvider =>
                serviceProvider.GetRequiredService<CreaturesDiedSimulationEventHandler>()
            )
            .AddTransient<CreatureEquipmentChangedSimulationEventHandler>()
            .AddTransient<IDomainEventConsumer<CreatureEquipmentChangedEvent>>(serviceProvider =>
                serviceProvider.GetRequiredService<CreatureEquipmentChangedSimulationEventHandler>()
            )
            .AddTransient<QuestMarkersChangedSimulationEventHandler>()
            .AddTransient<IDomainEventConsumer<QuestMarkersChangedEvent>>(serviceProvider =>
                serviceProvider.GetRequiredService<QuestMarkersChangedSimulationEventHandler>()
            )
            .AddTransient<DoorLocksChangedSimulationEventHandler>()
            .AddTransient<IDomainEventConsumer<DoorLocksChangedEvent>>(serviceProvider =>
                serviceProvider.GetRequiredService<DoorLocksChangedSimulationEventHandler>()
            )
            .AddTransient<CreaturesSpawnedSimulationEventHandler>()
            .AddTransient<IDomainEventConsumer<CreaturesSpawnedEvent>>(serviceProvider =>
                serviceProvider.GetRequiredService<CreaturesSpawnedSimulationEventHandler>()
            );
}
