using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Common.Events;
using TRPG.Application.CreatureJobs.EventHandlers;

namespace TRPG.Application.CreatureJobs.Extensions;

public static class CreatureJobsServiceCollectionExtensions
{
    public static IServiceCollection AddCreatureJobsServices(
        this IServiceCollection serviceCollection
    ) =>
        serviceCollection
            .AddTransient<CreatureFreedJobEventHandler>()
            .AddTransient<IDomainEventConsumer<CreatureFreedEvent>>(serviceProvider =>
                serviceProvider.GetRequiredService<CreatureFreedJobEventHandler>()
            );
}
