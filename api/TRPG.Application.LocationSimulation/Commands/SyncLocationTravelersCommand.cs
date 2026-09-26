using TRPG.Application.Common.Commands;
using TRPG.Application.Routing.Commands;
using TRPG.Domain;

namespace TRPG.Application.LocationSimulation.Commands;

public class SyncLocationTravelersCommand
{
    public required Guid WorldId { get; init; }
    public required Guid LocationId { get; init; }
    public required GameInstant GameTime { get; init; }
}

internal class SyncLocationTravelersCommandHandler(
    ICommandHandler<EnsureCreatureRouteSchedulesCommand> ensureCreatureRouteSchedules,
    ICommandHandler<
        MaterializeScheduledRouteTravelersCommand,
        IReadOnlyCollection<Guid>
    > materializeScheduledRouteTravelers,
    ICommandHandler<SyncRouteTravelersCommand> syncRouteTravelers
) : ICommandHandler<SyncLocationTravelersCommand>
{
    public async Task Handle(
        SyncLocationTravelersCommand command,
        CancellationToken cancellationToken = default
    )
    {
        await ensureCreatureRouteSchedules.Handle(
            new EnsureCreatureRouteSchedulesCommand { WorldId = command.WorldId },
            cancellationToken
        );

        await materializeScheduledRouteTravelers.Handle(
            new MaterializeScheduledRouteTravelersCommand
            {
                WorldId = command.WorldId,
                LocationId = command.LocationId,
                GameTime = command.GameTime,
            },
            cancellationToken
        );

        await syncRouteTravelers.Handle(
            new SyncRouteTravelersCommand
            {
                WorldId = command.WorldId,
                LocationId = command.LocationId,
                GameTime = command.GameTime,
            },
            cancellationToken
        );
    }
}
