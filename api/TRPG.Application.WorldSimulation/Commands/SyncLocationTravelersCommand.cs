using TRPG.Application.Common.Commands;
using TRPG.Domain;

namespace TRPG.Application.WorldSimulation.Commands;

public class SyncLocationTravelersCommand
{
    public required Guid WorldId { get; init; }
    public required Guid LocationId { get; init; }
    public required GameInstant GameTime { get; init; }
}

internal class SyncLocationTravelersCommandHandler(
    ICommandHandler<SyncRouteTravelersCommand> syncRouteTravelers
) : ICommandHandler<SyncLocationTravelersCommand>
{
    public async Task Handle(
        SyncLocationTravelersCommand command,
        CancellationToken cancellationToken = default
    )
    {
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
