using Microsoft.Extensions.DependencyInjection;

namespace TRPG.Application.LocationSimulation.Extensions;

public static class LocationSimulationServiceCollectionExtensions
{
    public static IServiceCollection AddLocationSimulationServices(
        this IServiceCollection serviceCollection
    ) => serviceCollection.AddSingleton<LocationCatchUpCache>();
}
