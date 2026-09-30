using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Common.Events;
using TRPG.Application.RoomBookings.EventHandlers;

namespace TRPG.Application.RoomBookings.Extensions;

public static class RoomBookingsServiceCollectionExtensions
{
    public static IServiceCollection AddRoomBookingsServices(
        this IServiceCollection serviceCollection
    ) =>
        serviceCollection
            .AddTransient<WorkstationRestockedReplacementKeyEventHandler>()
            .AddTransient<IDomainEventConsumer<WorkstationRestockedEvent>>(serviceProvider =>
                serviceProvider.GetRequiredService<WorkstationRestockedReplacementKeyEventHandler>()
            );
}
