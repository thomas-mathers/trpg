using Microsoft.Extensions.DependencyInjection;

namespace TRPG.Application.Routing.Extensions;

public static class RoutingServiceCollectionExtensions
{
    public static IServiceCollection AddRoutingServices(
        this IServiceCollection serviceCollection
    ) => serviceCollection;
}
