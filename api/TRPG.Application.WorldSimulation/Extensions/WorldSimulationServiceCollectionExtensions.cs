using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Common.Events;
using TRPG.Application.WorldSimulation.EventHandlers;

namespace TRPG.Application.WorldSimulation.Extensions;

public static class WorldSimulationServiceCollectionExtensions
{
    public static IServiceCollection AddWorldSimulationServices(
        this IServiceCollection serviceCollection
    ) =>
        serviceCollection
            .AddTransient<PlayerVitalsPublisher>()
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
            .AddScoped<WorldSimulatorLoader>()
            .AddScoped<WeatherExposureProvider>()
            .AddSingleton<WorldSimulationRunnerFactory>()
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
            .AddTransient<CreaturesSpawnedSimulationEventHandler>()
            .AddTransient<IDomainEventConsumer<CreaturesSpawnedEvent>>(serviceProvider =>
                serviceProvider.GetRequiredService<CreaturesSpawnedSimulationEventHandler>()
            );
}
