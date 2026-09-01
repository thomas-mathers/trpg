using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Common.Events;
using TRPG.Application.Reputations.EventHandlers;

namespace TRPG.Application.Reputations.Extensions;

public static class ReputationsServiceCollectionExtensions
{
    public static IServiceCollection AddReputationsServices(
        this IServiceCollection serviceCollection
    ) =>
        serviceCollection
            .AddTransient<QuestReputationRewardedEventHandler>()
            .AddTransient<IDomainEventConsumer<QuestReputationRewardedEvent>>(serviceProvider =>
                serviceProvider.GetRequiredService<QuestReputationRewardedEventHandler>()
            );
}
