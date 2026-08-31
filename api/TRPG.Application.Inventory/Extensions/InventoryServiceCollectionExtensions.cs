using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Common.Events;
using TRPG.Application.Inventory.Events;

namespace TRPG.Application.Inventory.Extensions;

public static class InventoryServiceCollectionExtensions
{
    public static IServiceCollection AddInventoryServices(
        this IServiceCollection serviceCollection
    ) =>
        serviceCollection
            .AddTransient<TradeOfferValidator>()
            .AddTransient<TradeOfferEvaluator>()
            .AddTransient<QuestItemGuard>()
            .AddTransient<QuestGoldRewardedEventHandler>()
            .AddTransient<IDomainEventConsumer<QuestGoldRewardedEvent>>(serviceProvider =>
                serviceProvider.GetRequiredService<QuestGoldRewardedEventHandler>()
            );
}
