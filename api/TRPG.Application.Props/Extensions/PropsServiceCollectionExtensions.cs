using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Common.Events;
using TRPG.Application.Props.EventHandlers;

namespace TRPG.Application.Props.Extensions;

public static class PropsServiceCollectionExtensions
{
    public static IServiceCollection AddPropsServices(this IServiceCollection serviceCollection) =>
        serviceCollection
            .AddTransient<PlayerMovedSeatEventHandler>()
            .AddTransient<IDomainEventConsumer<PlayerMovedEvent>>(serviceProvider =>
                serviceProvider.GetRequiredService<PlayerMovedSeatEventHandler>()
            )
            .AddTransient<CreaturesStartedWalkingPropsEventHandler>()
            .AddTransient<IDomainEventConsumer<CreaturesStartedWalkingEvent>>(serviceProvider =>
                serviceProvider.GetRequiredService<CreaturesStartedWalkingPropsEventHandler>()
            )
            .AddTransient<CreaturesFellAsleepPropsEventHandler>()
            .AddTransient<IDomainEventConsumer<CreaturesFellAsleepEvent>>(serviceProvider =>
                serviceProvider.GetRequiredService<CreaturesFellAsleepPropsEventHandler>()
            )
            .AddTransient<CreaturesDiedPropsEventHandler>()
            .AddTransient<IDomainEventConsumer<CreaturesDiedEvent>>(serviceProvider =>
                serviceProvider.GetRequiredService<CreaturesDiedPropsEventHandler>()
            )
            .AddTransient<CreaturesWokePropsEventHandler>()
            .AddTransient<IDomainEventConsumer<CreaturesWokeEvent>>(serviceProvider =>
                serviceProvider.GetRequiredService<CreaturesWokePropsEventHandler>()
            );
}
