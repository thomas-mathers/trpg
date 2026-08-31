using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Common.Events;
using TRPG.Application.Quests.EventHandlers;

namespace TRPG.Application.Quests.Extensions;

public static class QuestsServiceCollectionExtensions
{
    public static IServiceCollection AddQuestServices(this IServiceCollection serviceCollection) =>
        serviceCollection
            .AddTransient<QuestObjectiveAdvancer>()
            .AddTransient<CreatureKilledQuestEventHandler>()
            .AddTransient<IDomainEventConsumer<CreatureKilledEvent>>(serviceProvider =>
                serviceProvider.GetRequiredService<CreatureKilledQuestEventHandler>()
            )
            .AddTransient<CreatureKilledQuestGiverEventHandler>()
            .AddTransient<IDomainEventConsumer<CreatureKilledEvent>>(serviceProvider =>
                serviceProvider.GetRequiredService<CreatureKilledQuestGiverEventHandler>()
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
