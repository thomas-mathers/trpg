using Microsoft.Extensions.DependencyInjection;

namespace TRPG.Application.Trading;

public static class TradingServiceCollectionExtensions
{
    public static IServiceCollection AddTradingServices(
        this IServiceCollection serviceCollection
    ) =>
        serviceCollection
            .AddTransient<InventoryItemTransfer>()
            .AddTransient<TradeOfferValidator>()
            .AddTransient<TradeOfferEvaluator>();
}
