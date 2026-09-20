using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Common.Events;
using TRPG.Application.Quests.EventHandlers;

namespace TRPG.Application.Quests.Extensions;

public static class QuestsServiceCollectionExtensions
{
    public static IServiceCollection AddQuestServices(this IServiceCollection serviceCollection) =>
        serviceCollection
            .AddTransient<QuestObjectiveAdvancer>()
            .AddTransient<FactDisclosureResolver>()
            .AddTransient<FactDisclosureAttemptRecorder>()
            .AddTransient<FactLearnedQuestEventHandler>()
            .AddTransient<IDomainEventConsumer<FactLearnedEvent>>(serviceProvider =>
                serviceProvider.GetRequiredService<FactLearnedQuestEventHandler>()
            )
            .AddTransient<FactLearnedReportQuestEventHandler>()
            .AddTransient<IDomainEventConsumer<FactLearnedEvent>>(serviceProvider =>
                serviceProvider.GetRequiredService<FactLearnedReportQuestEventHandler>()
            )
            .AddTransient<CreatureKilledQuestEventHandler>()
            .AddTransient<IDomainEventConsumer<CreatureKilledEvent>>(serviceProvider =>
                serviceProvider.GetRequiredService<CreatureKilledQuestEventHandler>()
            )
            .AddTransient<CreatureKilledQuestGiverEventHandler>()
            .AddTransient<IDomainEventConsumer<CreatureKilledEvent>>(serviceProvider =>
                serviceProvider.GetRequiredService<CreatureKilledQuestGiverEventHandler>()
            )
            .AddTransient<PlayerMovedQuestEventHandler>()
            .AddTransient<ItemAcquiredQuestEventHandler>()
            .AddTransient<IDomainEventConsumer<ItemAcquiredEvent>>(serviceProvider =>
                serviceProvider.GetRequiredService<ItemAcquiredQuestEventHandler>()
            )
            .AddTransient<ItemGivenToCreatureQuestEventHandler>()
            .AddTransient<IDomainEventConsumer<ItemGivenToCreatureEvent>>(serviceProvider =>
                serviceProvider.GetRequiredService<ItemGivenToCreatureQuestEventHandler>()
            )
            .AddTransient<TriggerActivatedQuestEventHandler>()
            .AddTransient<IDomainEventConsumer<TriggerActivatedEvent>>(serviceProvider =>
                serviceProvider.GetRequiredService<TriggerActivatedQuestEventHandler>()
            )
            .AddTransient<QuestInteractablePropCleaner>()
            .AddTransient<IDomainEventConsumer<PlayerMovedEvent>>(serviceProvider =>
                serviceProvider.GetRequiredService<PlayerMovedQuestEventHandler>()
            )
            .AddTransient<CreatureFreedQuestEventHandler>()
            .AddTransient<IDomainEventConsumer<CreatureFreedEvent>>(serviceProvider =>
                serviceProvider.GetRequiredService<CreatureFreedQuestEventHandler>()
            )
            .AddTransient<FactDisclosedQuestEventHandler>()
            .AddTransient<IDomainEventConsumer<NpcFactDisclosedEvent>>(serviceProvider =>
                serviceProvider.GetRequiredService<FactDisclosedQuestEventHandler>()
            )
            .AddTransient<FactDisclosureLockoutResetEventHandler>()
            .AddTransient<IDomainEventConsumer<QuestCompletedEvent>>(serviceProvider =>
                serviceProvider.GetRequiredService<FactDisclosureLockoutResetEventHandler>()
            )
            .AddTransient<QuestCompletedExclusiveGroupEventHandler>()
            .AddTransient<IDomainEventConsumer<QuestCompletedEvent>>(serviceProvider =>
                serviceProvider.GetRequiredService<QuestCompletedExclusiveGroupEventHandler>()
            );
}
