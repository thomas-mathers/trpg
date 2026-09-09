using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Common.Events;
using TRPG.Application.Worlds.EventHandlers;

namespace TRPG.Application.Worlds.Extensions;

public static class WorldsServiceCollectionExtensions
{
    public static IServiceCollection AddWorldsServices(this IServiceCollection serviceCollection) =>
        serviceCollection
            .AddTransient<PlayerMovedDungeonPremiseEventHandler>()
            .AddTransient<IDomainEventConsumer<PlayerMovedEvent>>(serviceProvider =>
                serviceProvider.GetRequiredService<PlayerMovedDungeonPremiseEventHandler>()
            );
}
