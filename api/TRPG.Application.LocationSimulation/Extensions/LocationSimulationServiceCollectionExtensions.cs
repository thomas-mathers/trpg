using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Common.Events;
using TRPG.Application.LocationSimulation.EventHandlers;

namespace TRPG.Application.LocationSimulation.Extensions;

public static class LocationSimulationServiceCollectionExtensions
{
    public static IServiceCollection AddLocationSimulationServices(
        this IServiceCollection serviceCollection
    ) =>
        serviceCollection
            .AddSingleton<LocationCatchUpCache>()
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
            );
}
