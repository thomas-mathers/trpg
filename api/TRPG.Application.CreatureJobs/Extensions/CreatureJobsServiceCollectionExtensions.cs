using Microsoft.Extensions.DependencyInjection;

namespace TRPG.Application.CreatureJobs.Extensions;

public static class CreatureJobsServiceCollectionExtensions
{
    public static IServiceCollection AddCreatureJobsServices(
        this IServiceCollection serviceCollection
    ) => serviceCollection;
}
