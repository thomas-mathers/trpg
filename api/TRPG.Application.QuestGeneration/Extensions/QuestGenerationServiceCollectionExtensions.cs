using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Common.Events;
using TRPG.Application.QuestGeneration.EventHandlers;

namespace TRPG.Application.QuestGeneration.Extensions;

public static class QuestGenerationServiceCollectionExtensions
{
    public static IServiceCollection AddQuestGenerationServices(
        this IServiceCollection serviceCollection
    ) =>
        serviceCollection
            .AddTransient<QuestCompletedFactionChainSeedEventHandler>()
            .AddTransient<IDomainEventConsumer<QuestCompletedEvent>>(serviceProvider =>
                serviceProvider.GetRequiredService<QuestCompletedFactionChainSeedEventHandler>()
            );
}
