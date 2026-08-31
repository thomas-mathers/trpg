using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Chat.EventHandlers;
using TRPG.Application.Common.Events;

namespace TRPG.Application.Chat.Extensions;

public static class ChatServiceCollectionExtensions
{
    public static IServiceCollection AddChatServices(this IServiceCollection serviceCollection) =>
        serviceCollection
            .AddTransient<GameSessionCreatedEventHandler>()
            .AddTransient<IDomainEventConsumer<GameSessionCreatedEvent>>(serviceProvider =>
                serviceProvider.GetRequiredService<GameSessionCreatedEventHandler>()
            );
}
