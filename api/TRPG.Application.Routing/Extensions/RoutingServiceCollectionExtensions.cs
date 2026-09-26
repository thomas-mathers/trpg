using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Common.Events;
using TRPG.Application.Routing.EventHandlers;

namespace TRPG.Application.Routing.Extensions;

public static class RoutingServiceCollectionExtensions
{
    public static IServiceCollection AddRoutingServices(
        this IServiceCollection serviceCollection
    ) =>
        serviceCollection
            .AddTransient<CreaturesEngagedEventHandler>()
            .AddTransient<IDomainEventConsumer<CreaturesEngagedEvent>>(serviceProvider =>
                serviceProvider.GetRequiredService<CreaturesEngagedEventHandler>()
            );
}
