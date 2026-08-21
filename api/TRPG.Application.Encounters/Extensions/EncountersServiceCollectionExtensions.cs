using Microsoft.Extensions.DependencyInjection;

namespace TRPG.Application.Encounters.Extensions;

public static class EncountersServiceCollectionExtensions
{
    public static IServiceCollection AddEncountersServices(
        this IServiceCollection serviceCollection
    ) => serviceCollection.AddSingleton<ITheftDetectionRoller, TheftDetectionRoller>();
}
