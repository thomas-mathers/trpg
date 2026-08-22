using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Common.Events;
using TRPG.Application.Reputations.Events;

namespace TRPG.Application.Reputations.Extensions;

public static class ReputationsServiceCollectionExtensions
{
    public static IServiceCollection AddReputationsServices(
        this IServiceCollection serviceCollection
    ) =>
        serviceCollection
            .AddTransient<CreatureKilledCrimeWitnessEventHandler>()
            .AddTransient<IDomainEventConsumer<CreatureKilledEvent>>(serviceProvider =>
                serviceProvider.GetRequiredService<CreatureKilledCrimeWitnessEventHandler>()
            );
}
