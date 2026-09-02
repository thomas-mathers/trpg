using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Common.Events;
using TRPG.Application.NpcConversations.EventHandlers;

namespace TRPG.Application.NpcConversations.Extensions;

public static class NpcConversationsServiceCollectionExtensions
{
    public static IServiceCollection AddNpcConversationsServices(
        this IServiceCollection serviceCollection
    ) =>
        serviceCollection
            .AddTransient<GameSessionDeletedEventHandler>()
            .AddTransient<IDomainEventConsumer<GameSessionDeletedEvent>>(serviceProvider =>
                serviceProvider.GetRequiredService<GameSessionDeletedEventHandler>()
            );
}
