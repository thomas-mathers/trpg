using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Common.Events;
using TRPG.Application.Creatures;
using TRPG.Application.Creatures.EventHandlers;

namespace TRPG.Application.Creatures.Extensions;

public static class CreaturesServiceCollectionExtensions
{
    public static IServiceCollection AddCreaturesServices(
        this IServiceCollection serviceCollection
    ) =>
        serviceCollection
            .AddSingleton<IChanceRoller, ChanceRoller>()
            .AddTransient<SkillCheckService>()
            .AddTransient<SneakDetectionService>()
            .AddTransient<CreatureEquipmentChangedEventHandler>()
            .AddTransient<IDomainEventConsumer<CreatureEquipmentChangedEvent>>(serviceProvider =>
                serviceProvider.GetRequiredService<CreatureEquipmentChangedEventHandler>()
            );
}
