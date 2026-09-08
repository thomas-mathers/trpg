using Microsoft.Extensions.DependencyInjection;

namespace TRPG.Application.Encounters.Extensions;

public static class EncountersServiceCollectionExtensions
{
    public static IServiceCollection AddEncountersServices(
        this IServiceCollection serviceCollection
    ) =>
        serviceCollection
            .AddTransient<TheftSourceResolver>()
            .AddTransient<LocationCityResolver>()
            .AddTransient<WrongedFactionResolver>()
            .AddTransient<CombatantFactory>()
            .AddTransient<ActiveFightCombatantLoader>();
}
