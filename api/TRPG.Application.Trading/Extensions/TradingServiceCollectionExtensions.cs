using Microsoft.Extensions.DependencyInjection;

namespace TRPG.Application.Trading.Extensions;

public static class TradingServiceCollectionExtensions
{
    public static IServiceCollection AddTradingServices(
        this IServiceCollection serviceCollection
    ) =>
        serviceCollection
            .AddTransient<TradeOfferValidator>()
            .AddTransient<TradeOfferEvaluator>()
            .AddTransient<QuestItemGuard>();
}
