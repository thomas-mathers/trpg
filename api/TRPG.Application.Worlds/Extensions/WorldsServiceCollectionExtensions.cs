using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TRPG.Application.Common.Clocks;
using TRPG.Application.Common.Concurrency;
using TRPG.Application.Common.Events;
using TRPG.Application.Worlds;
using TRPG.Application.Worlds.EventHandlers;

namespace TRPG.Application.Worlds.Extensions;

public static class WorldsServiceCollectionExtensions
{
    public static IServiceCollection AddWorldsServices(this IServiceCollection serviceCollection) =>
        serviceCollection
            .AddWorldClock()
            .AddTransient<PlayerMovedDungeonPremiseEventHandler>()
            .AddTransient<IDomainEventConsumer<PlayerMovedEvent>>(serviceProvider =>
                serviceProvider.GetRequiredService<PlayerMovedDungeonPremiseEventHandler>()
            );

    private static IServiceCollection AddWorldClock(this IServiceCollection serviceCollection)
    {
        serviceCollection.TryAddSingleton(TimeProvider.System);
        serviceCollection.TryAddSingleton<IWorldClock, WorldClock>();
        serviceCollection.TryAddSingleton<IWorldMutationGate, WorldMutationGate>();
        return serviceCollection;
    }
}
