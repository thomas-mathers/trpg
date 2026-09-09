using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Common.Events;
using TRPG.Application.Knowledge.EventHandlers;

namespace TRPG.Application.Knowledge.Extensions;

public static class KnowledgeServiceCollectionExtensions
{
    public static IServiceCollection AddKnowledgeServices(
        this IServiceCollection serviceCollection
    ) =>
        serviceCollection
            .AddTransient<PlayerMovedRoomVisitEventHandler>()
            .AddTransient<IDomainEventConsumer<PlayerMovedEvent>>(serviceProvider =>
                serviceProvider.GetRequiredService<PlayerMovedRoomVisitEventHandler>()
            );
}
