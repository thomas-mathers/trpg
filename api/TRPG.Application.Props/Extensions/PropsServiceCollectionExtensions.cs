using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Common.Events;
using TRPG.Application.Props.EventHandlers;

namespace TRPG.Application.Props.Extensions;

public static class PropsServiceCollectionExtensions
{
    public static IServiceCollection AddPropsServices(this IServiceCollection serviceCollection) =>
        serviceCollection
            .AddTransient<PlayerMovedSeatEventHandler>()
            .AddTransient<IDomainEventConsumer<PlayerMovedEvent>>(serviceProvider =>
                serviceProvider.GetRequiredService<PlayerMovedSeatEventHandler>()
            );
}
