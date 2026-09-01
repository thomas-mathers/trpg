using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Common.Events;
using TRPG.Application.Crimes.EventHandlers;

namespace TRPG.Application.Crimes.Extensions;

public static class CrimesServiceCollectionExtensions
{
    public static IServiceCollection AddCrimesServices(this IServiceCollection serviceCollection) =>
        serviceCollection
            .AddTransient<CreatureKilledCrimeWitnessEventHandler>()
            .AddTransient<IDomainEventConsumer<CreatureKilledEvent>>(serviceProvider =>
                serviceProvider.GetRequiredService<CreatureKilledCrimeWitnessEventHandler>()
            )
            .AddTransient<PendingCrimeWitnessResolutionService>();
}
