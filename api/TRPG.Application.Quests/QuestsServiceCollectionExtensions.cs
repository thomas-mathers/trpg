using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Common.Events;
using TRPG.Application.Quests.Events;

namespace TRPG.Application.Quests;

public static class QuestsServiceCollectionExtensions
{
    public static IServiceCollection AddQuestServices(this IServiceCollection serviceCollection) =>
        serviceCollection
            .AddTransient<QuestObjectiveAdvancer>()
            .AddTransient<CreatureKilledQuestEventHandler>()
            .AddTransient<IDomainEventConsumer<CreatureKilledEvent>>(serviceProvider =>
                serviceProvider.GetRequiredService<CreatureKilledQuestEventHandler>()
            )
            .AddTransient<PlayerMovedQuestEventHandler>()
            .AddTransient<ConversationStartedQuestEventHandler>()
            .AddTransient<IDomainEventConsumer<NpcConversationStartedEvent>>(serviceProvider =>
                serviceProvider.GetRequiredService<ConversationStartedQuestEventHandler>()
            )
            .AddTransient<ItemAcquiredQuestEventHandler>()
            .AddTransient<IDomainEventConsumer<ItemAcquiredEvent>>(serviceProvider =>
                serviceProvider.GetRequiredService<ItemAcquiredQuestEventHandler>()
            )
            .AddTransient<IDomainEventConsumer<PlayerMovedEvent>>(serviceProvider =>
                serviceProvider.GetRequiredService<PlayerMovedQuestEventHandler>()
            );
}
