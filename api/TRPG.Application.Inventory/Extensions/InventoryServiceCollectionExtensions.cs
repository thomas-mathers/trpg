using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Common.Events;
using TRPG.Application.Inventory.EventHandlers;

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
            .AddTransient<GoldLoader>()
            .AddTransient<EquipmentLoadoutLoader>()
            .AddTransient<QuestGoldRewardedEventHandler>()
            .AddTransient<IDomainEventConsumer<QuestGoldRewardedEvent>>(serviceProvider =>
                serviceProvider.GetRequiredService<QuestGoldRewardedEventHandler>()
            );
}
