using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Common.Events;
using TRPG.Application.Crimes.EventHandlers;
using TRPG.Application.Crimes.Resolvers;

namespace TRPG.Application.Crimes.Extensions;

public static class CrimesServiceCollectionExtensions
{
    public static IServiceCollection AddCrimesServices(this IServiceCollection serviceCollection) =>
        serviceCollection
            .AddTransient<CreatureKilledCrimeWitnessEventHandler>()
            .AddTransient<IDomainEventConsumer<CreatureKilledEvent>>(serviceProvider =>
                serviceProvider.GetRequiredService<CreatureKilledCrimeWitnessEventHandler>()
            )
            .AddTransient<PlayerMovedCrimeConsequencesEventHandler>()
            .AddTransient<IDomainEventConsumer<PlayerMovedEvent>>(serviceProvider =>
                serviceProvider.GetRequiredService<PlayerMovedCrimeConsequencesEventHandler>()
            )
            .AddTransient<PendingCrimeWitnessResolutionService>()
            .AddTransient<KillConsequenceResolver>()
            .AddTransient<ICrimeConsequenceResolver>(serviceProvider =>
                serviceProvider.GetRequiredService<KillConsequenceResolver>()
            )
            .AddTransient<AssaultConsequenceResolver>()
            .AddTransient<ICrimeConsequenceResolver>(serviceProvider =>
                serviceProvider.GetRequiredService<AssaultConsequenceResolver>()
            )
            .AddTransient<TheftConsequenceResolver>()
            .AddTransient<ICrimeConsequenceResolver>(serviceProvider =>
                serviceProvider.GetRequiredService<TheftConsequenceResolver>()
            )
            .AddTransient<LockpickingConsequenceResolver>()
            .AddTransient<ICrimeConsequenceResolver>(serviceProvider =>
                serviceProvider.GetRequiredService<LockpickingConsequenceResolver>()
            )
            .AddTransient<TrespassingConsequenceResolver>()
            .AddTransient<ICrimeConsequenceResolver>(serviceProvider =>
                serviceProvider.GetRequiredService<TrespassingConsequenceResolver>()
            );
}
